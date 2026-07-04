# 双人合作攀岩 MVP 网络协议规范

## 目的

本文档定义 Anchor 双人合作攀岩 MVP 的网络协议约定。当前目标是用轻量 WebSocket relay 跑通双人房间、房主开始和客户端自权威角色同步。

服务器只负责连接、房间、基础校验和消息转发。攀岩玩法数据放在 `payload` 中，由客户端按玩法需求自定义和解释。`game.*` payload 结构通过 JSON 配置维护，修改该配置不需要重新部署服务器。

## 身份策略

MVP 不需要创建角色流程，也不直接使用设备 ID。

- `playerId`：服务器在连接建立后临时分配，用于当前连接、房间房主、玩家列表和消息路由。
- `clientId`：客户端首次启动时生成 UUID 并保存在本地，用于调试或后续轻量重连预留。

MVP 不提供昵称设置交互。房间界面可以直接显示 `playerId`、`host` / `guest`，或由客户端本地随机生成展示名。展示名不进入核心协议，不作为房主、房间、同步或权限判断依据。

推荐流程：

1. 客户端启动时读取本地 `clientId`。
2. 如果本地没有 `clientId`，生成 UUID 并保存。
3. 客户端连接 relay 后发送可选 hello 信息，可以包含 `clientId` 和客户端版本。
4. 服务器返回 `system.welcome`，包含本次连接的 `playerId`。
5. 后续消息路由以服务器记录的 `playerId` 为准。

不要把设备 ID 作为 MVP 身份来源。设备 ID 会引入隐私、权限、平台兼容和合规成本。

## 连接地址

MVP 内测可以使用明文 WebSocket：

```text
ws://43.156.16.10:8080/ws
```

如果后续配置 nginx 反代，也可以收敛为不带端口的 `/ws`：

```text
ws://43.156.16.10/ws
```

明文 WebSocket 只能用于内部 MVP，不承载账号密码、支付、正式 token 或隐私数据。正式外测或上线前再配置域名和 `wss://`。

## 统一消息信封

所有消息使用统一 JSON 信封：

```json
{
  "type": "game.state",
  "requestId": "req-001",
  "roomId": "AB12",
  "senderId": "player-1",
  "seq": 12,
  "sentAt": 123456.78,
  "schema": "player-state.v1",
  "payload": {}
}
```

字段约定：

- `type`：消息类型，使用命名空间，例如 `system.welcome`、`room.create`、`game.state`。
- `requestId`：可选，请求/响应配对用，由客户端生成。
- `roomId`：房间相关消息携带。创建房间前可以为空。
- `senderId`：发送方玩家 ID。服务器转发时以服务器记录为准。
- `seq`：发送方递增序号，用于调试、丢弃旧状态或估算丢包。
- `sentAt`：发送方时间戳，用于延迟估算和插值。
- `schema`：payload 的数据版本，例如 `player-state.v1`、`climb-event.v1`。
- `payload`：具体业务内容，玩法模块可自定义。

## 扩展规则

- 服务器固定理解 `system.*` 和 `room.*`。
- 服务器对 `game.*` 只做基础校验和原样转发。
- 新增攀岩同步字段时，优先只修改客户端 payload 和本文档。
- 新增玩法消息时，优先使用 `game.*` 类型或升级 `schema`。
- payload 新字段默认可选；删除或改名字段时升级 `schema`。
- 新增房间规则、匹配、账号、重连或服务器校验时，才需要改服务器。

## 前端协议配置

Unity/Tuanjie 客户端应保留一份可编辑 JSON 协议定义配置，例如：

```text
docs/coop-network-protocol.config.json
```

建议配置内容：

- 消息类型，例如 `game.state`、`game.event`。
- schema 名称，例如 `player-state.v1`。
- payload 字段说明。
- 发送频率，例如 `10Hz`、事件触发、手动发送。
- 是否允许 relay 转发。
- 调试显示名称。

运行时代码应使用统一 `NetworkMessage` 信封。具体玩法模块只负责构造和读取 payload，避免在多个脚本里硬编码协议字段。

修改 JSON 配置中的 `game.*` payload 字段、schema 示例、发送频率或调试显示名，不需要重新部署服务器；客户端重新加载配置或重新出包即可。

需要重新部署服务器的情况只包括：

- 修改 `system.*` 或 `room.*` 控制流程。
- 修改服务器校验规则。
- 修改房间状态机。
- 修改端口、反代、日志、限制等部署配置。

## 系统消息

### `system.welcome`

服务器在连接建立后发送。

```json
{
  "type": "system.welcome",
  "payload": {
    "playerId": "p_8f3a2c",
    "serverTime": 123456.78
  }
}
```

### `system.error`

服务器返回错误。

```json
{
  "type": "system.error",
  "requestId": "req-002",
  "payload": {
    "code": "ROOM_FULL",
    "message": "房间已满"
  }
}
```

常用错误码：

- `INVALID_MESSAGE`：消息格式错误。
- `ROOM_NOT_FOUND`：房间不存在或已关闭。
- `ROOM_FULL`：房间已满。
- `NOT_HOST`：非房主不能开始。
- `INVALID_ROOM_STATE`：当前房间状态不允许该操作。

## 房间消息

### `room.list`

客户端请求当前可加入房间。

```json
{
  "type": "room.list",
  "requestId": "req-101",
  "payload": {}
}
```

### `room.list.result`

服务器返回当前房间列表。列表可以包含等待中、开局中和游戏中的房间；客户端根据 `canJoin` 决定是否显示加入按钮。

```json
{
  "type": "room.list.result",
  "requestId": "req-101",
  "payload": {
    "rooms": [
      {
        "roomId": "AB12",
        "hostId": "p_8f3a2c",
        "playerCount": 1,
        "maxPlayers": 2,
        "state": "lobby",
        "canJoin": true
      }
    ]
  }
}
```

房间列表只用于大厅展示和加入入口。MainLevel 开局中的玩家生成、上下出生点和本地/远端关系 MUST 使用房间内 `room.updated.players` 与 `hostId` 推导，不应只依赖房间列表中的 `playerCount`。

### `room.list.updated`

服务器在房间创建、加入、离开、开始或进入游戏时向所有在线客户端广播当前房间列表，用于刷新房间列表界面。

```json
{
  "type": "room.list.updated",
  "payload": {
    "rooms": [
      {
        "roomId": "AB12",
        "hostId": "p_8f3a2c",
        "playerCount": 2,
        "maxPlayers": 2,
        "state": "inGame",
        "canJoin": false
      }
    ]
  }
}
```

### `room.create`

客户端创建房间。

```json
{
  "type": "room.create",
  "requestId": "req-201",
  "payload": {}
}
```

### `room.created`

服务器返回创建结果。

```json
{
  "type": "room.created",
  "requestId": "req-201",
  "roomId": "AB12",
  "payload": {
    "roomId": "AB12",
    "hostId": "p_8f3a2c",
    "state": "lobby"
  }
}
```

### `room.join`

客户端加入房间。

```json
{
  "type": "room.join",
  "requestId": "req-301",
  "roomId": "AB12",
  "payload": {}
}
```

### `room.updated`

服务器向房间内玩家广播房间状态。该消息 MUST 包含 `hostId` 和 `players` 列表，客户端用它确定 MainLevel 开局身份：

- `hostId` 对应玩家固定为先锋攀登者，使用配置中的 HostStartPoint，左右手磁点初始吸附到 `ScatterAnchor_007` / `ScatterAnchor_008`。
- 非房主玩家固定为第二攀登者，使用配置中的 GuestStartPoint，左右手磁点初始吸附到 `ScatterAnchor_001` / `ScatterAnchor_002`。
- 客户端进入 MainLevel 后 MUST 找到对应 StartPoint 作为身体出生位置，并使用该槽位配置的左右手目标抓点初始化 `LeftHandMagnet` / `RightHandMagnet`。
- 单机直接进入 MainLevel 且没有房间身份时，客户端 MUST 按房主 / `host` 规则初始化。
- 该映射由房间身份决定，不随某个客户端本地视角变化。也就是说，非房主在自己的客户端虽然是本地可控玩家，也仍然生成在下方；房主作为远端玩家显示在上方。

```json
{
  "type": "room.updated",
  "roomId": "AB12",
  "payload": {
    "roomId": "AB12",
    "hostId": "p_8f3a2c",
    "state": "lobby",
    "players": [
      { "playerId": "p_8f3a2c", "role": "host", "isHost": true },
      { "playerId": "p_14bc90", "role": "guest", "isHost": false }
    ]
  }
}
```

### `room.start`

房主请求开始游戏。

```json
{
  "type": "room.start",
  "requestId": "req-401",
  "roomId": "AB12",
  "payload": {}
}
```

### `room.starting`

服务器通知双方准备进入游戏。

```json
{
  "type": "room.starting",
  "roomId": "AB12",
  "payload": {
    "countdownMs": 2000,
    "startAt": 123458.78
  }
}
```

### `room.enteredGame`

客户端完成场景切换后通知服务器。

```json
{
  "type": "room.enteredGame",
  "roomId": "AB12",
  "payload": {}
}
```

### `room.inGame`

服务器确认房间进入游戏同步阶段。

```json
{
  "type": "room.inGame",
  "roomId": "AB12",
  "payload": {}
}
```

客户端 MUST 在收到当前房间的 `room.inGame` 后再开始发送 MainLevel 玩法 `game.state` 或 `game.event`。场景已经加载但尚未收到 `room.inGame` 时，可以完成本地对象准备，但不得开始玩法同步。

### `room.leave` / `room.peerLeft`

客户端离开房间，服务器通知另一名玩家。

```json
{
  "type": "room.peerLeft",
  "roomId": "AB12",
  "payload": {
    "playerId": "p_14bc90"
  }
}
```

## 玩法消息

### `game.state`

客户端发送本地角色状态。MainLevel 攀爬同步使用 `climb-player-state.v1`，payload 由本地只读状态采样接口生成；网络层只读取本地主角状态，不修改本地主角输入、受力、耐力或攀爬状态机。

```json
{
  "type": "game.state",
  "roomId": "AB12",
  "seq": 34,
  "sentAt": 123456.78,
  "schema": "climb-player-state.v1",
  "payload": {
    "playerId": "p_8f3a2c",
    "slot": "host",
    "climbRole": "lead",
    "position": { "x": 0.1, "y": 2.3, "z": 0.0 },
    "rotationY": 90,
    "movementState": "climbing",
    "leftHandGripId": "hold-03",
    "rightHandGripId": "hold-02",
    "stamina": 0.8,
    "isFalling": false
  }
}
```

远端客户端收到 `game.state` 后 MUST 忽略来自自身 `playerId` 的回环消息，按 `senderId` / `playerId` 路由到远端只读攀爬骨架，并丢弃同一远端玩家的旧 `seq` 状态。

### `game.event`

客户端按需发送一次性动作事件。MainLevel 使用 `climb-event.v1` 作为框架；只有事件存在明确远端表现、UI 或调试需求时才需要发送。无独立表现需求且可由下一帧 `game.state` 表达的动作，可以不发送事件。

```json
{
  "type": "game.event",
  "roomId": "AB12",
  "seq": 35,
  "sentAt": 123457.12,
  "schema": "climb-event.v1",
  "payload": {
    "eventId": "evt-0001",
    "eventType": "fall.start",
    "actorPlayerId": "p_8f3a2c",
    "data": {
      "reason": "stamina"
    }
  }
}
```

`eventId` 用于远端去重。打钉和回收会改变双方共享的铆钉、库存和绳索路径，必须作为成功结果事件同步。失败尝试不发送成功事件。服务器只转发 `game.event`，不理解或重算铆钉规则。

#### `rivet.place`

本地客户端在打钉成功后发送，远端收到后创建或显示对应已部署铆钉，并重算只读绳索路径。

```json
{
  "type": "game.event",
  "roomId": "AB12",
  "seq": 36,
  "sentAt": 123457.25,
  "schema": "climb-event.v1",
  "payload": {
    "eventId": "evt-rivet-0001",
    "eventType": "rivet.place",
    "actorPlayerId": "p_8f3a2c",
    "data": {
      "rivetId": "rivet-03",
      "position": { "x": 0.2, "y": 4.8, "z": 0.0 },
      "inventoryAfter": 3,
      "ropeRevision": 12
    }
  }
}
```

#### `rivet.collect`

本地客户端在回收成功后发送，远端收到后移除对应已部署铆钉，更新库存显示，并按剩余铆钉重算只读绳索路径。

```json
{
  "type": "game.event",
  "roomId": "AB12",
  "seq": 37,
  "sentAt": 123458.10,
  "schema": "climb-event.v1",
  "payload": {
    "eventId": "evt-rivet-0002",
    "eventType": "rivet.collect",
    "actorPlayerId": "p_14bc90",
    "data": {
      "rivetId": "rivet-03",
      "inventoryAfter": 2,
      "ropeRevision": 13
    }
  }
}
```

远端客户端 MUST 按 `eventId` 去重，并 SHOULD 使用 `ropeRevision` 或等价序号处理重复/旧事件，避免重复打钉或重复回收。

#### `rivet.leadSwitch`

当前领攀者成功换领后发送，远端收到后交换领攀/后攀身份，但不移动任何玩家库存。初始 Lead/Second 由服务器房间身份决定：`hostId` 对应先锋攀登者，非房主对应第二攀登者；进入玩法后换领属于共享玩法状态，必须通过该事件同步。

```json
{
  "type": "game.event",
  "roomId": "AB12",
  "seq": 38,
  "sentAt": 123459.20,
  "schema": "climb-event.v1",
  "payload": {
    "eventId": "evt-rivet-0003",
    "eventType": "rivet.leadSwitch",
    "actorPlayerId": "lead",
    "data": {
      "inventoryAfter": 0,
      "ropeRevision": 14
    }
  }
}
```

客户端 UI 只应向当前本地玩家也是模型当前 `LeadPlayerId` 的一端显示换领入口；非领攀者不显示换领按钮。换领成功后，原第二攀登者成为新的领攀者，原领攀者成为新的后攀者，双方各自携带的铆钉数量保持不变。

## 房间流程

1. 客户端连接 WebSocket。
2. 服务器发送 `system.welcome`，分配 `playerId`。
3. 客户端发送 `room.list`，显示当前可加入房间。
4. 房主发送 `room.create`。
5. 服务器返回 `room.created`，客户端进入简单房间界面。
6. 第二名玩家发送 `room.join`。
7. 服务器向双方广播 `room.updated`。
8. 房间满员后，房主发送 `room.start`。
9. 服务器校验房主身份和房间状态，广播 `room.starting`。
10. 两个客户端按倒计时切换到 MainLevel。
11. 客户端发送 `room.enteredGame`。
12. 服务器收到双方确认后广播 `room.inGame`。
13. 双方根据 `room.updated.players` 确认房主为上方先锋攀登者、非房主为下方第二攀登者；房主使用 HostStartPoint 且左右手磁点吸附 `ScatterAnchor_007/008`，非房主使用 GuestStartPoint 且左右手磁点吸附 `ScatterAnchor_001/002`。
14. 双方开始发送 `game.state`，并按需发送 `game.event`。

## Demo 场景验收流程

本次 MVP 的工程验收标准是能进入 MainLevel，完整验证登录、建房、加入房间、开局和基础攀爬状态同步。

建议 demo 流程：

1. 启动两个客户端实例，进入网络 demo 入口界面。
2. 两个客户端连接 relay，并收到 `system.welcome`。
3. 客户端 A 创建房间，进入简单房间界面。
4. 客户端 B 刷新房间列表或输入房间码，加入客户端 A 的房间。
5. 房间满员后，客户端 A 作为房主点击开始。
6. 两个客户端收到 `room.starting`，进入同一个 MainLevel。
7. 两个客户端发送 `room.enteredGame`，服务器广播 `room.inGame`。
8. MainLevel 中显示本地可控攀爬角色和远端只读攀爬骨架。
9. 房主固定使用 HostStartPoint 与 `ScatterAnchor_007/008`，非房主固定使用 GuestStartPoint 与 `ScatterAnchor_001/002`，双方通过 `LeftHandMagnet` / `RightHandMagnet` 完成初始手部吸附。
10. 本地玩家通过 JSON 配置定义的 `climb-player-state.v1` payload 发送基础状态。
11. 远端客户端收到并显示对方位置、朝向、攀爬状态、左右手抓点和耐力。
12. 若当前接入了 `climb-event.v1`，触发一个有表现或调试需求的事件，另一端能收到并在 UI、日志或表现中显示。

验收通过条件：

- 两个客户端都能完成连接并获得不同 `playerId`。
- 客户端 A 能创建房间，客户端 B 能加入房间。
- 未满员时不能开始，满员后只有房主能开始。
- 两个客户端能进入同一个 MainLevel。
- 房主在两端都映射为 HostStartPoint 与 `ScatterAnchor_007/008`，非房主在两端都映射为 GuestStartPoint 与 `ScatterAnchor_001/002`。
- `game.state` 能通过 relay 转发并驱动远端表现。
- 如实现接入 `game.event`，至少一个 `climb-event.v1` 能通过 JSON 配置定义并完成端到端收发与去重。

## 服务器部署指引

MVP 推荐使用单进程 relay。服务器不保存长期数据，房间列表保存在内存中。

部署步骤：

1. SSH 登录服务器 `43.156.16.10`。
2. 确认系统版本、CPU 架构、内存、nginx 配置目录和可用运行时。
3. 创建服务目录，例如 `/opt/anchor-relay`。
4. 上传或拉取 relay 服务代码。
5. 安装运行时依赖。
6. 启动服务监听本地端口，例如 `127.0.0.1:8080`。
7. 配置 nginx 将 `/ws` 反代到 relay 服务，并启用 WebSocket upgrade。
8. 从开发机连接 `ws://43.156.16.10:8080/ws`，确认收到 `system.welcome`。
9. 配置 systemd 或简单脚本保活服务。
10. 查看日志，确认房间列表、创建房间、加入房间、房主开始和转发消息都能记录。

nginx 反代要点：

- 保留 `Upgrade` 和 `Connection` 头。
- 反代目标指向 relay 监听地址。
- MVP 可以先只使用 `80` 和明文 `ws://`。

## 服务器变更边界

不需要改服务器的情况：

- 新增 `game.state` payload 字段。
- 新增 `game.event` payload 字段。
- 升级 `player-state.v1` 到 `player-state.v2`。
- 调整客户端发送频率或插值策略。

需要改服务器的情况：

- 新增 `room.*` 控制消息。
- 新增匹配、账号、重连或观战。
- 需要服务器校验玩法规则。
- 需要持久化房间或玩家数据。

## 文档维护规则

- 协议字段变更时，先更新本文档。
- 客户端协议定义配置必须与本文档保持一致。
- 服务器只应依赖本文档中的信封字段、房间字段和控制消息。
- 玩法 payload 示例不是固定格式，具体以当前玩法模块配置为准。
