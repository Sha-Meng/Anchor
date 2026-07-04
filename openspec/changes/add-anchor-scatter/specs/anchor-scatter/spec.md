## ADDED Requirements

### Requirement: 打点锚点半径可编辑

关卡动线编辑器在打点时 MUST 为节点挂上 `AnchorPoint`，并 MUST 允许策划在窗口中设置新点的"核心半径"与"最大半径"，分别写入 `AnchorPoint.previewIntenseRadius` 与 `AnchorPoint.previewSlightRadius`。窗口设置作为新点默认值，MUST NOT 覆盖策划事后在 Inspector 对单个点的手工调整。

#### Scenario: 打点使用窗口设定的半径

- **WHEN** 策划在窗口设置核心半径与最大半径后，在墙面打出一个新点
- **THEN** 该点 MUST 带有 `AnchorPoint`，且其核心半径、最大半径与窗口设定值一致

#### Scenario: 半径取值被规整

- **WHEN** 策划把核心半径设为大于最大半径，或设为负值
- **THEN** 工具 SHOULD 将半径规整为非负，并保证核心半径不大于最大半径

### Requirement: 沿动线离散撒点

工具 MUST 提供"沿动线离散撒点"操作，以当前动线网络中所有 `RouteNode` 的有向边为骨架，在动线周围随机生成一批 `AnchorPoint`。撒点范围 MUST 由"散布带宽"约束在动线周围，生成的锚点 MUST 挂 `AnchorPoint` 并写入窗口设定的核心 / 最大半径。当动线网络中不存在任何边时，工具 MUST NOT 生成锚点。

#### Scenario: 基于动线生成锚点

- **WHEN** 场景中已存在若干由动线编辑器打出、并以有向边相连的 `RouteNode`，策划点击"沿动线离散撒点"
- **THEN** 工具 MUST 在这些动线边周围生成一批 `AnchorPoint`，且这些锚点集中分布在动线附近而非整个场景

#### Scenario: 无动线时不撒点

- **WHEN** 当前动线网络中没有任何 `RouteNode` 有向边
- **THEN** 工具 MUST NOT 生成任何锚点

### Requirement: 撒点必须附着在墙面

离散撒点生成的每个 `AnchorPoint` MUST 附着在带 `Wall` 脚本的墙面上：工具 MUST 用射线把候选点吸附到墙面命中点，命中碰撞体能取到 `Wall` 组件时才生成锚点，否则 MUST 丢弃该候选。

#### Scenario: 候选点吸附到墙面

- **WHEN** 一个候选点位于带 `Wall` 脚本的墙面附近
- **THEN** 工具 MUST 将该锚点落在墙面命中点（沿墙面法线做微小偏移），使其贴合墙面

#### Scenario: 未命中墙面的候选被丢弃

- **WHEN** 一个候选点吸附射线未命中任何碰撞体，或命中的碰撞体取不到 `Wall` 组件
- **THEN** 工具 MUST NOT 在该处生成锚点

### Requirement: 离散距离控制疏密

工具 MUST 提供"点与点之间的离散距离"参数，作为撒出锚点之间的最小间距。任意两个由本次撒点生成的锚点之间的距离 MUST NOT 小于该离散距离。调小该参数 MUST 使撒点分布更密，调大 MUST 使其更稀。

#### Scenario: 满足最小间距

- **WHEN** 策划设定离散距离为 D 并执行撒点
- **THEN** 本次生成的任意两个锚点之间的距离 MUST 不小于 D

#### Scenario: 疏密随参数变化

- **WHEN** 策划先用较小的离散距离撒点，再用较大的离散距离在相同动线上撒点
- **THEN** 较小离散距离生成的锚点数量 SHOULD 不少于较大离散距离生成的数量

### Requirement: 撒点可持久化与可撤销

离散撒点生成的锚点 MUST 以场景 GameObject 形式持久化并随场景保存，统一挂在自动创建的 `RouteNetwork/ScatterAnchors` 容器下。撒点与清空撒点操作 MUST 接入 Undo，并标记场景为已修改。清空撒点 MUST 只删除撒点容器，MUST NOT 影响手工打出的动线节点。

#### Scenario: 撤销撒点

- **WHEN** 策划执行撒点后按下撤销（Ctrl+Z）
- **THEN** 本次撒点生成的锚点 MUST 被回退

#### Scenario: 清空撒点不影响动线

- **WHEN** 策划点击"清空撒点"
- **THEN** `RouteNetwork/ScatterAnchors` 下的锚点 MUST 被删除，而手工打出的 `RouteNode` 动线节点 MUST 保留
