## ADDED Requirements

### Requirement: 小球控制状态

`ControllerMgr` MUST 为每个小球维护独立状态。状态 MUST 包含锚定中、释放中、Hook 中。指针正在控制小球时，小球 MUST 处于 Hook 中；Hook 被玩家松手结束或 Hook 中耐力耗尽时，系统 MUST 执行一次 Hook 结束结算；结算为锚点命中时，小球 MUST 处于锚定中，结算为脱离锚点时，小球 MUST 处于释放中。非 Hook 状态下，系统 MUST NOT 反复执行是否进入释放中的判定。

#### Scenario: 指针开始控制小球

- **WHEN** 玩家按住屏幕并命中可控制表面，且命中点更接近某个小球
- **THEN** 该小球 MUST 进入 Hook 中，并跟随命中点移动

#### Scenario: 待机小球被点击唤起

- **WHEN** 小球处于释放中/待机中，玩家再次按住屏幕并命中可控制表面，且命中点更接近该小球
- **THEN** 该小球 MUST 进入 Hook 中
- **AND** 该小球 MUST 停止继续移动到旧的待机目标
- **AND** 若该小球耐力已空，系统 SHOULD 为本次唤起补充一段可配置的最低耐力

#### Scenario: 耗尽体力断开控制

- **WHEN** 小球处于 Hook 中且耐力降至 0
- **THEN** 该小球 MUST 断开当前指针控制
- **AND** 系统 MUST 执行一次 Hook 结束结算，只有结算为脱离锚点时才进入释放中

### Requirement: 松手锚定结算

Hook 被玩家松手结束，或 Hook 中耐力耗尽时，`ControllerMgr` MUST 按小球当前位置与最近 `AnchorPoint` 的距离进行结算。`previewIntenseRadius` MUST 作为核心区域半径，`previewSlightRadius` MUST 作为外环最大范围。

#### Scenario: 核心区域松手

- **WHEN** 玩家松手时小球位于任一 `AnchorPoint` 的核心区域内
- **THEN** 小球 MUST 进入锚定中
- **AND** 小球 MUST NOT 因本次松手额外损失耐力

#### Scenario: 外环区域松手

- **WHEN** 玩家松手时小球位于任一 `AnchorPoint` 的核心区域外、外环最大范围内
- **THEN** 小球 MUST 进入锚定中
- **AND** 小球 MUST 损失 30% 最大耐力

#### Scenario: 脱离锚点松手

- **WHEN** 玩家松手时小球不在任何 `AnchorPoint` 最大范围内
- **THEN** 小球 MUST 进入释放中
- **AND** 小球 MUST 损失 50% 最大耐力

### Requirement: 释放待机位置

小球进入释放中时，`ControllerMgr` MUST 计算释放待机位置。待机位置的屏幕参考点 MUST 位于两个小球屏幕中点的下方；从该屏幕参考点发射射线后，首个有效碰撞点 MUST 作为待机位置。

#### Scenario: 释放后移动到待机点

- **WHEN** 小球进入释放中且待机射线命中有效碰撞体
- **THEN** 小球 SHOULD 平滑移动到该碰撞点

#### Scenario: 释放待机射线未命中

- **WHEN** 小球进入释放中且待机射线没有命中有效碰撞体
- **THEN** 小球 SHOULD 保持当前位置，且仍处于释放中

### Requirement: 三态磁点跟随速度

`HandFollowController` MUST 支持为锚定中、释放中、Hook 中分别配置磁点跟随速度，并 MUST 按对应小球当前状态选择速度。

#### Scenario: Hook 中使用 Hook 速度

- **WHEN** A 或 B 球处于 Hook 中
- **THEN** 对应手部磁点 MUST 使用 Hook 中跟随速度追随该球

#### Scenario: 非 Hook 状态使用对应速度

- **WHEN** A 或 B 球处于锚定中或释放中
- **THEN** 对应手部磁点 MUST 使用该状态配置的跟随速度追随该球
