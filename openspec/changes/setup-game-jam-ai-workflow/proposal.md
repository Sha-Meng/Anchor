## Why

Anchor 是面向 game jam 的 Tuanjie Editor 1.9.3 项目，需要在很短周期内完成策划、交互、实现和验收闭环。当前项目已有 OpenSpec 与 Codely bridge 基础，但缺少可由 Cursor 直接理解和复用的 Editor MCP 接入方案，以及面向 jam 节奏的本地 skill 系统。

## What Changes

- 接入一个兼容 Unity 2022.3/Tuanjie 1.9.3 的社区 Unity MCP，并优先选择最小外部依赖、可回退的方案。
- 增加项目级 Cursor MCP 配置，使 Cursor 能在 Editor MCP 服务启动后发现工具。
- 建立 MCP 可行性验证标准：至少能确认服务配置、连接状态或明确阻塞条件；若 Editor 未运行或插件未解析完成，应记录下一步人工验证动作。
- 建立轻量本地 skill 系统，覆盖 game jam 方案收敛、交互设计、Tuanjie 程序落地和测试验收。
- 非目标：不修改当前游戏玩法、不引入 Unity 6 专用 API、不依赖已关闭的其他项目 MCP。

## Capabilities

### New Capabilities

- `game-jam-ai-workflow`: 定义 Anchor 在 game jam 场景下使用 MCP、OpenSpec 和本地 Cursor skills 辅助快速策划、实现与验收的工作流。

### Modified Capabilities

- 无。

## Impact

- 影响 `Packages/manifest.json`、`.cursor/mcp.json`、`.cursor/skills/` 和 `openspec/changes/setup-game-jam-ai-workflow/`。
- 可能新增社区 Unity MCP 包依赖；若该依赖无法在 Tuanjie 1.9.3 中解析，应撤回或替换为兼容方案。
- MCP 验证依赖 Tuanjie Editor 打开项目并完成包导入；Cursor 侧只负责配置和发现工具。
