## MODIFIED Requirements

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
