## ADDED Requirements

### Requirement: MainLevel 开局同步入口

系统 MUST 在双人房间完成房主开始流程后，将两个客户端带入同一个 MainLevel 主关卡，并在双方都完成场景进入确认后开始玩法同步。

#### Scenario: 房主开始后进入 MainLevel
- **WHEN** 房间已满员且房主发送合法 `room.start`
- **THEN** 服务器 MUST 向房间双方广播开始游戏消息，客户端 MUST 按该消息加载 MainLevel 主关卡

#### Scenario: 双方进入后开始同步
- **WHEN** 两个客户端都完成 MainLevel 加载并发送 `room.enteredGame`
- **THEN** 服务器 MUST 广播 `room.inGame`，客户端 MUST 在收到后开始发送和消费 `game.state` 与 `game.event`

#### Scenario: 未进入游戏前不发送玩法状态
- **WHEN** 客户端尚未收到当前房间的 `room.inGame`
- **THEN** 客户端 MUST NOT 发送 MainLevel 攀爬玩法状态消息

### Requirement: 房间玩家槽位与出生点绑定

系统 MUST 基于服务器房间信息为两名玩家分配一致的 `host` / `guest` 槽位，并使用槽位绑定 MainLevel 中的上下起攀主抓点配置、玩家标识和本地/远端对象关系。`host` MUST 固定作为先锋攀登者使用上方起攀主抓点，`guest` MUST 固定作为第二攀登者使用下方起攀主抓点；客户端 MUST 由主抓点和临近抓点推导左右手初始抓握和躯干位置。该映射 MUST 由房间身份决定，MUST NOT 由每个客户端本地的 1P / 2P 视角决定。

#### Scenario: 根据房主标识分配槽位
- **WHEN** 客户端收到包含 `hostId` 和玩家列表的房间状态
- **THEN** 客户端 MUST 将 `hostId` 对应玩家映射为 `host` 槽位和上方先锋攀登者起攀主抓点，并将另一名玩家映射为 `guest` 槽位和下方第二攀登者起攀主抓点

#### Scenario: 两端出生点一致
- **WHEN** 两个客户端进入 MainLevel 并完成槽位映射
- **THEN** 两端 MUST 使用同一套 `host` 到上方起攀主抓点、`guest` 到下方起攀主抓点的配置生成玩家对象，且两个玩家初始位置 MUST 不同

#### Scenario: 由主抓点推导合法初始姿态
- **WHEN** 客户端为某个槽位生成 MainLevel 玩家对象
- **THEN** 客户端 MUST 找到该槽位配置的起攀主抓点和临近抓点，并用这两个合法抓点分配左右手初始抓握和躯干位置

#### Scenario: 本地视角不改变开局身份
- **WHEN** 客户端自身是非房主但在本机作为本地可控玩家
- **THEN** 该客户端 MUST 仍将自身生成为下方第二攀登者，并将房主远端玩家显示为上方先锋攀登者

#### Scenario: 本地与远端关系派生
- **WHEN** 客户端完成槽位映射且知道自己的 `playerId`
- **THEN** 客户端 MUST 将自身 `playerId` 对应槽位生成为本地可控玩家，并将另一槽位生成为远端表现玩家

### Requirement: 本地玩家与远端玩家职责分离

系统 MUST 明确区分本地可控玩家与远端表现玩家。本地玩家由本机输入、攀爬 3C、受力和后续玩法系统驱动；远端玩家 MUST 复用攀爬角色骨架作为只读表现对象，只消费网络状态和必要事件，不参与本机输入或本机玩法判定。

#### Scenario: 本地玩家接收输入
- **WHEN** MainLevel 中生成本地玩家
- **THEN** 系统 MUST 为该玩家启用本机输入、相机跟随、攀爬控制和本地状态采样

#### Scenario: 远端玩家不接收输入
- **WHEN** MainLevel 中生成远端玩家
- **THEN** 系统 MUST 使用攀爬角色骨架生成远端只读表现玩家，禁用该玩家的本机输入、相机控制、受力判定、耐力消耗和本机玩法判定，并仅允许网络同步组件更新其表现

#### Scenario: 不反向判定远端玩法
- **WHEN** 本机收到远端玩家的 `game.state` 或 `game.event`
- **THEN** 系统 MUST 只更新远端表现对象或调试信息，MUST NOT 用本机抓点、受力或绳索规则重新判定远端玩家结果

### Requirement: 基础攀爬状态同步

系统 MUST 使用 `game.state` 同步本地玩家的基础攀爬状态。状态 payload MUST 至少表达玩家标识、槽位、位置、朝向、移动/攀爬状态、左右手抓点、耐力、坠落状态、发送序号和发送时间。网络层 MUST 通过只读状态采样接口读取本地攀爬状态，MUST NOT 直接修改本地主角控制、输入、受力或耐力逻辑。

#### Scenario: 发送本地攀爬状态
- **WHEN** 客户端处于 MainLevel 游戏同步阶段且本地玩家状态达到发送间隔
- **THEN** 客户端 MUST 发送 schema 为 `climb-player-state.v1` 的 `game.state` 消息，payload MUST 包含本地玩家基础攀爬状态

#### Scenario: 只读采样本地状态
- **WHEN** 网络同步模块需要发送本地玩家状态
- **THEN** 网络同步模块 MUST 通过 `IClimbStateSource`、`ClimbStateSnapshot` 或等价只读接口获取状态，并 MUST NOT 调用本地主角控制流程改变玩法结果

#### Scenario: 忽略自己的状态回环
- **WHEN** 客户端收到 `senderId` 等于自身 `playerId` 的 `game.state`
- **THEN** 客户端 MUST 忽略该消息并保持本地玩家权威状态不变

#### Scenario: 丢弃旧状态
- **WHEN** 客户端收到同一远端玩家的 `game.state` 且消息序号不大于已应用序号
- **THEN** 客户端 MUST 丢弃该状态，避免远端表现倒退

#### Scenario: 更新远端表现
- **WHEN** 客户端收到同房间另一名玩家的新 `game.state`
- **THEN** 客户端 MUST 将该状态应用到对应远端玩家表现对象，并 SHALL 使用插值或等价平滑策略降低抖动

### Requirement: 攀爬一次性事件同步

系统 SHALL 使用 `game.event` 同步基础攀爬中有明确远端表现、UI 或调试需求的一次性动作事件。事件 payload MUST 至少包含 `eventId`、`eventType`、`actorPlayerId` 和可选事件数据。没有独立表现需求、且可由下一帧 `game.state` 表达的动作 MAY 不发送事件。插锚、拔锚和收绳事件类型 SHALL 作为协议框架预留，并在铆钉绳索系统实现时定义具体发送时机、payload 和远端表现。

#### Scenario: 按表现需求发送事件
- **WHEN** 本地玩家完成一次需要远端立即播放表现、显示 UI 或记录调试日志的一次性动作
- **THEN** 客户端 SHALL 发送 schema 为 `climb-event.v1` 的 `game.event` 消息描述该动作

#### Scenario: 远端应用事件
- **WHEN** 客户端收到同房间另一名玩家的新 `game.event`
- **THEN** 客户端 MUST 仅在该事件对应明确表现、UI 或调试需求时，将该事件用于远端玩家表现、调试 UI 或日志反馈

#### Scenario: 事件去重
- **WHEN** 客户端收到已处理过 `eventId` 的 `game.event`
- **THEN** 客户端 MUST 忽略重复事件，避免重复播放关键反馈

#### Scenario: 绳索事件随绳索系统接入
- **WHEN** 铆钉绳索系统尚未提供插锚、拔锚或收绳的玩法结果
- **THEN** 网络同步系统 SHALL 保留 `game.event` 扩展框架，但 SHOULD NOT 编造这些事件的最终 payload 或远端表现

### Requirement: 远端玩家离开处理

系统 MUST 在房间内另一名玩家离开或断开时停止该远端玩家的网络更新，并提供清晰的 MVP 级反馈。

#### Scenario: 收到队友离开
- **WHEN** 客户端收到当前房间的 `room.peerLeft`
- **THEN** 客户端 MUST 标记远端玩家已离开，停止继续应用该玩家状态，并显示可调试反馈

#### Scenario: 离开后状态冻结
- **WHEN** 远端玩家已被标记为离开
- **THEN** 系统 SHALL 冻结、置灰、隐藏远端玩家或提供等价表现，并 MUST NOT 继续把旧网络状态当作在线玩家状态应用

### Requirement: 攀爬同步协议配置维护

系统 MUST 在协议文档和客户端 JSON 协议配置中维护 MainLevel 攀爬同步 payload 的 schema、字段、发送频率和调试显示信息。

#### Scenario: 配置攀爬状态 schema
- **WHEN** 开发者新增或调整 `climb-player-state.v1` 字段
- **THEN** 系统 MUST 更新协议文档和客户端 JSON 协议配置，并 MUST NOT 要求 relay 服务器理解具体攀爬字段

#### Scenario: 配置攀爬事件 schema
- **WHEN** 开发者新增或调整 `climb-event.v1` 事件类型或字段
- **THEN** 系统 MUST 更新协议文档和客户端 JSON 协议配置，并 MUST 保持 `game.event` 由 relay 基础校验后转发
