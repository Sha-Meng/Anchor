## ADDED Requirements

### Requirement: 社区 MCP 接入

项目 SHALL 提供一个面向 Tuanjie Editor 1.9.3 的社区 Unity MCP 接入配置，使 Cursor 能在 Editor 完成包解析和服务启动后连接 MCP 工具。

#### Scenario: Cursor 配置存在

- **WHEN** 开发者打开项目根目录
- **THEN** 项目应包含 `.cursor/mcp.json`，并配置名为 `mcp-unity` 的 MCP server

#### Scenario: Editor 包未解析

- **WHEN** Cursor 尝试启动 `mcp-unity` 但 `Library/PackageCache` 中没有 MCP Unity 包
- **THEN** 启动器应输出明确提示，要求先打开 Tuanjie Editor 等待 Package Manager 解析包

#### Scenario: Editor 包已解析

- **WHEN** `Library/PackageCache` 中存在 `com.gamelovers.mcp-unity` 包
- **THEN** 启动器应定位 `Server~`，并在缺少构建产物时尝试安装依赖和构建 Node MCP server

### Requirement: Game Jam 本地 Skills

项目 SHALL 提供轻量项目级 Cursor skills，用于辅助 game jam 的方案制定、交互设计、Tuanjie 程序落地和测试验收。

#### Scenario: 策划收敛

- **WHEN** 用户要求制定或收敛 game jam 玩法方案
- **THEN** Agent 应使用本地 skill 将想法压缩为核心循环、MVP、砍需求边界和验收目标

#### Scenario: 交互设计

- **WHEN** 用户要求设计玩法交互或 UI 流程
- **THEN** Agent 应使用本地 skill 输出输入、反馈、状态、UI/场景对象和可验证路径

#### Scenario: 程序落地

- **WHEN** 用户要求在 Tuanjie 项目中实现玩法或系统
- **THEN** Agent 应遵守 Tuanjie `2022.3.62t11` 约束，避免 Unity 6 专用 API，并优先使用项目已有模式

#### Scenario: 测试验收

- **WHEN** 用户要求验证玩法或准备提交 game jam build
- **THEN** Agent 应使用本地 skill 生成冒烟测试、手工验收、Unity Test Runner 或 MCP 截图检查方案
