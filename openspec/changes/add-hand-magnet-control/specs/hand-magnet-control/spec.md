## ADDED Requirements

### Requirement: hook 时恢复手部磁力

当玩家用鼠标点击或手指摁压屏幕 hook 某侧小球时，该侧手部磁点 MUST 恢复磁力（`RA2MagnetPoint.DragPower` 置为吸附锁定值），使对应手被吸附到磁点。hook / 松手判定 MUST 依据该侧半屏是否有指针按住（按住状态），MUST NOT 仅依据射线是否命中场景。

#### Scenario: 按住小球恢复磁力

- **WHEN** 玩家在某侧半屏按下（鼠标左键或手指）hook 该侧小球，且两球未超距
- **THEN** 该侧手部磁点的磁力 MUST 恢复到吸附锁定值，手被吸附

#### Scenario: 射线未命中不误判为松手

- **WHEN** 玩家持续按住某侧小球，但该帧射线未命中任何墙面
- **THEN** 系统 MUST 仍视为 hook 持续（按住状态为真），MUST NOT 判定为松手

### Requirement: 松手时按 AnchorPoint 区域决定磁力与耐力

当玩家松开对某侧小球的控制时，系统 MUST 计算该侧小球到最近 `AnchorPoint` 的三维距离，并以该最近锚点的核心半径（`previewIntenseRadius`）与外环半径（`previewSlightRadius`）分档处理：核心区域保持磁力，外环区域保持磁力但损失最大耐力的 30%，脱离锚点则取消磁力。耐力惩罚 MUST 通过事件方式作用于耐力系统，且在无耐力系统时静默跳过而不影响磁力判定。

#### Scenario: 松手于核心区域

- **WHEN** 玩家松手时该侧小球到最近锚点的距离不大于该锚点核心半径
- **THEN** 该侧手部磁力 MUST 保持吸附锁定值，MUST NOT 损失耐力

#### Scenario: 松手于外环区域

- **WHEN** 玩家松手时该侧小球到最近锚点的距离大于核心半径且不大于外环半径
- **THEN** 该侧手部磁力 MUST 保持吸附锁定值
- **AND** 耐力 MUST 一次性减少最大耐力的 30%

#### Scenario: 松手时脱离锚点

- **WHEN** 玩家松手时该侧小球到最近锚点的距离大于外环半径（或场景中无任何 `AnchorPoint`）
- **THEN** 该侧手部磁力 MUST 取消（`DragPower` 置为释放值），手脱落

### Requirement: 两球最大距离约束

`ControllerMgr` MUST 预设一个可配置字段表示两个小球之间的最大距离，并对外提供两球实时距离与超距查询。基于该最大距离：当某侧处于 hook 状态时，若两球距离超出最大距离的 10%，MUST 立刻断开该拖动侧手部磁力；玩家按下 / 摁压屏幕时，若两球距离已超出最大距离，该侧手部磁力 MUST NOT 恢复。距离约束触发的断磁力 MUST 只作用于当前正在被按压 / 拖动的那一侧手。

#### Scenario: hook 中超出最大距离 10% 断磁力

- **WHEN** 某侧小球处于 hook 状态，且两球距离超过最大距离的 110%
- **THEN** 该拖动侧手部磁力 MUST 立刻取消
- **AND** 另一只已锚定（未被按压）的手 MUST NOT 因此断磁力

#### Scenario: 按下时已超距不恢复磁力

- **WHEN** 玩家按下某侧小球时，两球距离已超过最大距离
- **THEN** 该侧手部磁力 MUST NOT 恢复（保持取消状态）

#### Scenario: 最大距离可配置

- **WHEN** 策划在 `ControllerMgr` 上修改两球最大距离字段
- **THEN** 超距判定 MUST 依据新的最大距离值
