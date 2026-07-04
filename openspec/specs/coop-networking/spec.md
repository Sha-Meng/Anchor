# Coop Networking Specification

## Purpose

该规格定义 Anchor 双人合作 MVP 的轻量联网能力，包括中继服务、房间流程、玩家身份、可扩展 JSON 协议、Unity/Tuanjie demo 验收与腾讯云部署边界。

## Requirements

### Requirement: MVP 轻量中继拓扑

系统 MUST 采用面向双人 MVP 的客户端-服务器中继拓扑，服务器只负责连接、房间列表、房间状态和消息转发，不计算攀岩物理或角色状态。

#### Scenario: 创建中继房间

- **WHEN** 一名玩家创建双人合作房间
- **THEN** 系统 MUST 在服务器侧创建房间记录并返回可分享或可输入的房间码

#### Scenario: 转发房间消息

- **WHEN** 房间内一名玩家发送合法同步消息
- **THEN** 系统 MUST 将该消息转发给同房间内另一名玩家

### Requirement: 当前房间列表

系统 MUST 在服务器内存中维护当前可加入房间列表，并允许客户端查询该列表。

#### Scenario: 查询可加入房间

- **WHEN** 客户端请求当前房间列表
- **THEN** 系统 MUST 返回未满员且处于等待状态的房间信息

#### Scenario: 房间状态变化

- **WHEN** 房间创建、玩家加入、玩家离开、游戏开始或房间关闭
- **THEN** 系统 MUST 更新当前房间列表中的对应房间状态

### Requirement: MVP 玩家身份

系统 MUST 在连接建立时由服务器分配临时 playerId，并 SHALL 允许客户端提供本地生成的可选 clientId 用于调试，不要求创建角色流程或昵称设置交互。

#### Scenario: 服务器分配玩家标识

- **WHEN** 客户端成功连接 MVP 中继服务
- **THEN** 服务器 MUST 返回包含 playerId 的欢迎消息

#### Scenario: 客户端首次启动生成本地标识

- **WHEN** 客户端本地不存在 clientId
- **THEN** 客户端 SHALL 生成 UUID 格式的 clientId 并保存在本地

#### Scenario: 不依赖设备 ID

- **WHEN** 客户端建立 MVP 联机连接
- **THEN** 系统 MUST NOT 要求读取或上传平台设备 ID

#### Scenario: 不设置昵称

- **WHEN** 玩家进入 MVP 联机流程
- **THEN** 系统 MUST NOT 要求玩家设置昵称后才能创建或加入房间

### Requirement: 双人房间生命周期

系统 MUST 支持创建房间、加入房间、进入简单房间界面、房主开始、进入游戏、离开房间和房间关闭的 MVP 生命周期，并 MUST 保证同一房间最多两名活跃玩家。

#### Scenario: 创建房间后进入房间界面

- **WHEN** 玩家成功创建房间
- **THEN** 系统 MUST 将该玩家设为房主并让客户端进入简单房间界面

#### Scenario: 第二名玩家加入房间

- **WHEN** 第二名玩家输入有效且未满员的房间码或从房间列表选择房间
- **THEN** 系统 MUST 将该玩家加入房间并通知双方房间信息已更新

#### Scenario: 房间满员

- **WHEN** 第三名玩家尝试加入已有两名活跃玩家的房间
- **THEN** 系统 MUST 拒绝加入并返回房间已满原因

#### Scenario: 房主开始游戏

- **WHEN** 房间已满员且房主点击开始
- **THEN** 系统 MUST 向双方广播开始游戏消息

#### Scenario: 非房主开始游戏

- **WHEN** 非房主客户端发送开始游戏请求
- **THEN** 系统 MUST 拒绝请求并返回非房主不可开始的错误原因

#### Scenario: 玩家离开房间

- **WHEN** 任一玩家主动断开连接或离开房间
- **THEN** 系统 MUST 通知另一名玩家队友已离开

### Requirement: 可扩展消息协议

系统 MUST 定义统一消息信封，使房间控制、角色状态、动作事件、心跳和错误反馈共享同一种外层格式，并允许玩法同步内容通过 payload 自定义扩展。

#### Scenario: 发送统一信封消息

- **WHEN** 客户端或服务器发送协议消息
- **THEN** 消息 MUST 使用包含 type、payload 和可选 requestId、roomId、senderId、seq、sentAt、schema 的统一信封格式

#### Scenario: 转发自定义 payload

- **WHEN** 客户端发送服务器不理解但允许转发的玩法 payload
- **THEN** 服务器 MUST 在校验房间归属和消息格式后将 payload 原样转发给另一名玩家

#### Scenario: 扩展玩法字段

- **WHEN** 后续玩法新增角色状态或动作事件字段
- **THEN** 系统 MUST 支持通过 payload 增加可选字段或通过 schema 增加新版本，而不要求修改服务器转发逻辑

### Requirement: 前端协议定义配置

系统 SHALL 在 Unity/Tuanjie 客户端提供可编辑的 JSON 协议定义配置，用于维护 game.* 消息类型、schema、payload 字段说明、发送频率和调试显示信息。

#### Scenario: 编辑玩法同步结构

- **WHEN** 开发者需要新增或调整攀岩玩法同步字段
- **THEN** 开发者 SHOULD 能通过客户端 JSON 协议定义配置调整 payload 结构说明，而不修改服务器转发逻辑

#### Scenario: 修改 game payload 配置

- **WHEN** 开发者只修改 game.* payload 字段、schema 示例、发送频率或调试显示名
- **THEN** 系统 MUST NOT 要求重新部署服务器

#### Scenario: 维护协议规范文档

- **WHEN** 协议字段、消息类型或房间流程发生变化
- **THEN** 系统 MUST 更新工程协议规范文档以保持实现与文档一致

#### Scenario: 限制服务器变更范围

- **WHEN** 新增 game.* 玩法同步消息或 schema 版本
- **THEN** 系统 MUST 允许服务器在完成基础校验后原样转发消息 payload

### Requirement: 客户端自权威角色状态

系统 SHALL 让每个客户端计算自己控制角色的状态，并将该状态作为该角色在 MVP 中的权威状态同步给另一名玩家。

#### Scenario: 本地玩家状态上报

- **WHEN** 本地玩家角色状态需要同步
- **THEN** 客户端 MUST 使用统一信封发送包含玩法自定义 payload 的角色状态消息

#### Scenario: 远端玩家状态显示

- **WHEN** 客户端收到另一名玩家的角色状态消息
- **THEN** 客户端 MUST 使用消息 payload 更新远端玩家表现对象

### Requirement: 腾讯云 MVP 部署

系统 SHALL 支持在腾讯云服务器 `43.156.16.10` 上部署 MVP 中继服务，并 MUST 能通过公网客户端完成连接、房间列表、创建房间、加入房间、房主开始和消息转发测试。

#### Scenario: 使用明文 WebSocket 测试

- **WHEN** MVP 不承载账号密码、支付、正式 token 或隐私数据
- **THEN** 系统 MAY 使用明文 WebSocket 完成内部联机测试

#### Scenario: 客户端连接中继服务

- **WHEN** MVP 中继服务已在腾讯云服务器上启动
- **THEN** 客户端 MUST 能通过配置的服务器地址建立连接

#### Scenario: 验证房间联机流程

- **WHEN** 两个客户端连接到腾讯云中继服务
- **THEN** 系统 MUST 能完成查询房间列表、创建房间、加入房间、房主开始和进入游戏流程

### Requirement: MVP 错误反馈与基础日志

系统 MUST 提供最小错误反馈和基础日志，用于定位连接、房间和消息格式问题。

#### Scenario: 房间码无效

- **WHEN** 玩家输入不存在或已关闭的房间码
- **THEN** 系统 MUST 返回房间不存在或不可加入的错误原因

#### Scenario: 记录房间事件

- **WHEN** 玩家连接、创建房间、加入房间、开始游戏、离开房间或发送非法消息
- **THEN** 服务器 MUST 记录包含时间、连接标识、房间标识和事件类型的日志

### Requirement: Demo 场景端到端验收

系统 MUST 提供基础 demo 场景验证流程，用于验证连接、创建房间、加入房间、房主开局、进入游戏和自定义 game.* 协议交互。

#### Scenario: 完成房间到游戏流程

- **WHEN** 两个客户端连接 MVP 中继服务并完成创建房间、加入房间和房主开始
- **THEN** 两个客户端 MUST 能进入同一个 demo 游戏场景

#### Scenario: 验证基础状态同步

- **WHEN** 两个客户端处于 demo 游戏场景
- **THEN** 系统 MUST 能通过 JSON 配置定义的 game.state payload 完成本地到远端的状态转发和显示

#### Scenario: 验证自定义事件同步

- **WHEN** demo 场景触发一个 JSON 配置定义的自定义 game.event
- **THEN** 另一端客户端 MUST 能收到该事件并在 UI 或日志中显示

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

系统 MUST 基于服务器房间信息为两名玩家分配一致的 `host` / `guest` 槽位，并使用槽位绑定 MainLevel 中的场景 StartPoint、左右手磁点目标抓点、玩家标识和本地/远端对象关系。`host` MUST 固定作为先锋攀登者使用 StartPoint2，且 `LeftHandMagnet` / `RightHandMagnet` 初始吸附到 `ScatterAnchor_007` / `ScatterAnchor_008`；`guest` MUST 固定作为第二攀登者使用 StartPoint1，且 `LeftHandMagnet` / `RightHandMagnet` 初始吸附到 `ScatterAnchor_001` / `ScatterAnchor_002`。该映射 MUST 由房间身份决定，MUST NOT 由每个客户端本地的 1P / 2P 视角决定。单机直接进入 MainLevel 且没有房间身份时，系统 MUST 按 `host` 规则初始化本地玩家。

#### Scenario: 根据房主标识分配槽位
- **WHEN** 客户端收到包含 `hostId` 和玩家列表的房间状态
- **THEN** 客户端 MUST 将 `hostId` 对应玩家映射为 `host` 槽位和 StartPoint2，并将另一名玩家映射为 `guest` 槽位和 StartPoint1

#### Scenario: 两端出生点一致
- **WHEN** 两个客户端进入 MainLevel 并完成槽位映射
- **THEN** 两端 MUST 使用同一套 `host` 到 StartPoint2、`guest` 到 StartPoint1 的配置生成玩家对象，且两个玩家初始位置 MUST 不同

#### Scenario: 左右手磁点吸附到槽位抓点
- **WHEN** 客户端为某个槽位生成 MainLevel 本地可控玩家对象
- **THEN** 客户端 MUST 创建或使用该玩家的 `LeftHandMagnet` / `RightHandMagnet`，并将其初始吸附到该槽位配置的左右手目标抓点

#### Scenario: 房主左右手初始抓点
- **WHEN** 客户端为 `host` 槽位生成 MainLevel 玩家对象
- **THEN** `LeftHandMagnet` MUST 初始吸附到 `ScatterAnchor_007`，`RightHandMagnet` MUST 初始吸附到 `ScatterAnchor_008`

#### Scenario: 非房主左右手初始抓点
- **WHEN** 客户端为 `guest` 槽位生成 MainLevel 玩家对象
- **THEN** `LeftHandMagnet` MUST 初始吸附到 `ScatterAnchor_001`，`RightHandMagnet` MUST 初始吸附到 `ScatterAnchor_002`

#### Scenario: 单机按房主位置初始化
- **WHEN** 开发者或玩家直接进入 MainLevel 且当前没有联网房间身份
- **THEN** 系统 MUST 按 `host` 槽位初始化本地玩家，使用 StartPoint2 和 `ScatterAnchor_007` / `ScatterAnchor_008`

#### Scenario: 本地视角不改变开局身份
- **WHEN** 客户端自身是非房主但在本机作为本地可控玩家
- **THEN** 该客户端 MUST 仍将自身生成为 `guest` / 第二攀登者，并将房主远端玩家显示为 `host` / 先锋攀登者

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
