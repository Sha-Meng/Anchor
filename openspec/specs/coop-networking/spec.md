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
