## Context

Anchor 使用 Tuanjie Editor 1.9.3，底层版本为 `2022.3.62t11`。Unity 官方 MCP 文档面向 Unity 6 与 `com.unity.ai.assistant`，不适合作为当前项目的直接接入路径。项目已有 `cn.tuanjie.codely.bridge@1.0.66`，但公开资料未确认它能让 Cursor 作为外部 MCP client 直接控制 Editor。

当前目标是给 game jam 开发建立一个可快速验证、可回退的外部 Editor MCP 接入，并在此基础上建立轻量本地 skill 系统。

## Goals / Non-Goals

**Goals:**

- 使用兼容 Unity 2022.3/Tuanjie 1.9 的社区 MCP，而不是 Unity 6 专用官方 MCP。
- 让 Cursor 项目级 MCP 配置具备可移植性，不绑定某个本机 PackageCache hash。
- 在 Editor 尚未运行或包未解析时提供明确失败信息。
- 建立少量、高触发价值的 game jam skills，覆盖策划、交互、实现和验收。

**Non-Goals:**

- 不改动当前游戏玩法、场景或资源。
- 不引入 GPL 编辑器包作为首选依赖。
- 不依赖已关闭的其他项目 MCP。
- 不追求一次性自动完成所有 Editor 内配置；需要 Editor 参与的步骤允许人工打开 Editor 完成。

## Decisions

### 选择 `MaansenV/mcp-unity` 作为第一接入对象

该包声明支持 Unity `2022.3+`，许可证为 MIT，运行时需要 Node.js `18+`。当前机器已有 Node `v24.12.0`，满足要求。相比 `emeryporter/UnityMCP`，它避免了 GPL-3.0-or-later 许可证风险；相比 `CoplayDev/unity-mcp`，它不需要当前机器缺失的 `uv`。

### 使用 UPM Git 依赖接入 Unity 包

在 `Packages/manifest.json` 中加入：

`com.gamelovers.mcp-unity`: `https://github.com/MaansenV/mcp-unity.git?path=/Packages/mcp-unity`

这样 Tuanjie Editor 打开项目后由 Package Manager 解析包。若 Tuanjie 包管理器无法解析该 Git 依赖，可删除该依赖或改用手动 Package Manager 导入。

### 使用项目级 Cursor MCP 启动器

`.cursor/mcp.json` 调用 `.cursor/run-mcp-unity-server.mjs`，由脚本扫描 `Library/PackageCache/com.gamelovers.mcp-unity@*/Server~`。这样不需要把某个本机 hash 写进配置，也避免每次包更新后手动改路径。

启动器在 `build/index.js` 不存在时会在 `Server~` 下执行 `npm install` 和 `npm run build`。如果 PackageCache 不存在，脚本会提示先打开 Tuanjie Editor 等待包解析。

### 本地 skills 保持轻量

本地 skill 系统分为 4 个项目级 skills：

- `game-jam-planner`: 收敛主题、核心循环、MVP 和砍需求。
- `interaction-designer`: 把玩法转成输入、反馈、UI、状态机和验收路径。
- `tuanjie-implementer`: 在 Tuanjie 2022.3 约束下实现，不使用 Unity 6 专用 API。
- `jam-test-acceptance`: 生成手工验收、Unity Test Runner 验证和截图/冒烟检查。

## Risks / Trade-offs

- 社区 MCP 在 Tuanjie 1.9 中编译失败 → 回退 `Packages/manifest.json` 中的 Git 依赖，改试 CoplayDev 或 Codely 官方能力。
- `com.unity.test-framework@1.3.3` 依赖在 Tuanjie registry 不可解析 → 优先检查 Package Manager 错误，再决定固定旧版本或换 MCP。
- Cursor 启动 MCP 时 PackageCache 尚未生成 → 启动器明确报错，要求先打开 Editor 完成包解析。
- Node server 首次构建耗时或网络失败 → 保留 `npm install`/`npm run build` 的 stderr，便于定位代理、registry 或网络问题。

## Migration Plan

1. 提交 UPM Git 依赖、Cursor MCP 配置和启动器。
2. 打开 Tuanjie Editor，等待 Package Manager 解析 `com.gamelovers.mcp-unity`。
3. 打开 `Tools > MCP Unity > Server Window`，确认 WebSocket server 端口 `8090` 可启动。
4. 重启 Cursor 或刷新 MCP，确认 `mcp-unity` server 可启动。
5. 若失败，按错误定位；若是包兼容问题，删除 manifest 依赖和 Cursor MCP 配置并切换候选。

## Open Questions

- Tuanjie 1.9 的 Package Manager 是否能顺利解析该 Git UPM 包及其 `com.unity.test-framework@1.3.3` 依赖。
- Cursor 当前版本对项目级 `.cursor/mcp.json` 的刷新是否需要重启窗口。
