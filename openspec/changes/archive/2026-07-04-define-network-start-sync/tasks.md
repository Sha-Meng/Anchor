## 1. 协议与配置准备

- [x] 1.1 更新 `docs/coop-network-protocol.md`，补充 MainLevel 开局、房主上方先锋攀/非房主下方第二攀登者映射、本地/远端玩家职责和离开处理说明
- [x] 1.2 更新 `docs/coop-network-protocol.config.json`，新增或替换 `climb-player-state.v1` 的 `game.state` payload 字段、发送频率和调试显示名
- [x] 1.3 更新 `docs/coop-network-protocol.config.json`，新增或替换按需使用的 `climb-event.v1` 的 `game.event` payload 字段、事件类型说明和调试显示名，并为插锚、拔锚、收绳保留后续扩展框架
- [x] 1.4 确认 relay 返回的 `room.updated` payload 包含 `hostId` 和 `players` 列表；若缺失，记录客户端 fallback 规则和后续服务器补齐点

## 2. 房间到 MainLevel 流程

- [x] 2.1 将默认联机游戏场景从 `NetworkDemoGame` 改为 `MainLevel`，或提供可配置的目标游戏场景字段
- [x] 2.2 在客户端保存当前房间的 `hostId`、玩家列表、自己的 `playerId`、`roomId` 和 `host` / `guest` 槽位，并派生房主=上方先锋攀、非房主=下方第二攀登者
- [x] 2.3 补齐 `room.updated` / `room.joined` / `room.created` 的玩家列表解析，确保两端槽位映射一致
- [x] 2.4 确保客户端只在收到当前房间 `room.inGame` 后开始发送 MainLevel `game.state` 和 `game.event`

## 3. MainLevel 玩家生成与绑定

- [x] 3.1 在配置中定义上方先锋攀起攀主抓点和下方第二攀登者起攀主抓点，并将 `host` 固定映射到上方、`guest` 固定映射到下方；进入后由主抓点和临近抓点推导合法初始位置，提供缺省 fallback
- [x] 3.2 实现 MainLevel 网络会话绑定入口，进入场景后生成本地可控玩家和远端表现玩家
- [x] 3.3 将本地玩家接入现有 `Climb3CLevelBinder` 或等价攀爬控制器，启用输入、相机、UI 和状态采样
- [x] 3.4 远端玩家复用攀爬角色骨架做只读表现，禁用本地输入、相机控制、受力判定、耐力消耗和本机玩法判定
- [x] 3.5 在 UI、材质、名称或调试文本中区分本地/远端以及 `host` / `guest`

## 4. 基础攀爬状态同步

- [x] 4.1 为本地攀爬角色提供 `IClimbStateSource` / `ClimbStateSnapshot` 或等价只读状态采样接口，输出位置、朝向、移动/攀爬状态、左右手抓点、耐力和坠落状态
- [x] 4.2 使用 `climb-player-state.v1` 构建 `game.state` payload，并按配置发送频率发送本地玩家状态
- [x] 4.3 接收 `game.state` 时忽略自己的消息，并按 `senderId` / 槽位路由到远端玩家
- [x] 4.4 对远端玩家状态按 `seq` 丢弃旧包，并使用插值或等价平滑策略更新远端表现
- [x] 4.5 在调试日志或 UI 中显示最近一次远端状态的 `senderId`、`seq`、延迟估算和主要攀爬状态

## 5. 攀爬事件同步

- [x] 5.1 定义按需使用的 `climb-event.v1` 事件构造方法，包含 `eventId`、`eventType`、`actorPlayerId` 和可选 `data`，并预留插锚、拔锚、收绳事件类型
- [x] 5.2 只为当前远端表现、UI 或调试确实需要的一次性动作发送 `game.event`，无独立表现需求的动作依赖 `game.state`
- [x] 5.3 接收远端 `game.event` 时按 `eventId` 去重，并仅驱动有明确需求的远端表现、调试 UI 或日志反馈；插锚、拔锚、收绳的具体 payload 与表现等待铆钉绳索系统接入

## 6. 离开与异常处理

- [x] 6.1 收到 `room.peerLeft` 后标记远端玩家离开，并停止应用该玩家后续状态
- [x] 6.2 为离开的远端玩家提供冻结、置灰、隐藏或等价 MVP 表现，并显示“队友离开”调试信息
- [x] 6.3 返回房间入口或离开房间时清理当前 MainLevel 网络状态，避免旧 `roomId`、槽位和远端对象污染下一局

## 7. 验收

- [x] 7.1 使用两个客户端完成连接、建房、加入、房主开始，并验证双方进入 `MainLevel`
- [ ] 7.2 验证两端 `host` / `guest` 槽位、房主上方先锋攀起攀抓点、非房主下方第二攀登者起攀抓点、本地可控玩家和远端只读攀爬骨架映射一致
- [ ] 7.3 操作任一客户端基础攀爬，验证另一端远端玩家能更新位置、朝向、攀爬状态、左右手抓点和耐力显示
- [ ] 7.4 若当前实现接入了 `climb-event.v1`，触发至少一个事件，验证另一端能收到并在表现、UI 或日志中体现且不会重复应用
- [ ] 7.5 让任一客户端离开房间，验证另一端停止更新远端玩家并显示离开反馈
- [x] 7.6 运行 OpenSpec 校验，确认本变更 proposal、design、specs 和 tasks 均可被识别
