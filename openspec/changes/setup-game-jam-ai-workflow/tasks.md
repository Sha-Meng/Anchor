## 1. 社区 MCP 接入

- [x] 1.1 调研并选择兼容 Tuanjie 1.9.3 / Unity 2022.3 的社区 MCP 候选
- [x] 1.2 避开当前缺失 `uv` 的 CoplayDev 路线和 GPL 许可证风险较高的候选
- [x] 1.3 在 `Packages/manifest.json` 添加 `com.gamelovers.mcp-unity` Git UPM 依赖
- [x] 1.4 添加项目级 `.cursor/mcp.json`
- [x] 1.5 添加 `.cursor/run-mcp-unity-server.mjs`，自动定位 PackageCache 并启动 Node MCP server

## 2. MCP 可行性验证

- [x] 2.1 验证 Node、Git 和项目 JSON/脚本语法
- [x] 2.2 在 Editor 尚未解析包时运行启动器，确认能输出明确阻塞提示
- [x] 2.3 打开 Tuanjie Editor，等待 Package Manager 解析 `com.gamelovers.mcp-unity`
- [x] 2.4 在 `Tools > MCP Unity > Server Window` 启动 WebSocket server，并确认端口 `8090` 连接状态
- [x] 2.5 重启或刷新 Cursor MCP，确认 `mcp-unity` server 可发现工具
- [x] 2.6 执行只读 smoke test：读取 Unity Console 或场景信息

## 3. 轻量本地 Skill 系统

- [x] 3.1 创建 `game-jam-planner` skill
- [x] 3.2 创建 `interaction-designer` skill
- [x] 3.3 创建 `tuanjie-implementer` skill
- [x] 3.4 创建 `jam-test-acceptance` skill
- [x] 3.5 验证每个 `SKILL.md` 的 frontmatter、触发描述和行数

## 4. 收尾

- [x] 4.1 运行 OpenSpec validate
- [x] 4.2 记录 MCP 验证结论和剩余阻塞
- [ ] 4.3 如果 MCP 包无法在 Tuanjie 1.9.3 中解析，撤回依赖并切换候选
