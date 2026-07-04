## Why

关卡动线编辑器（`route-editor`）目前只能在墙面逐个打点并连线，打出来的点没有半径信息，也无法批量填充可抓取的锚点。而 `AnchorPoint` 已经定义了两档半径（`previewIntenseRadius` 核心档、`previewSlightRadius` 最大档），是 `MouseFollowJitter` 抖动分档与后续 `level-anchor-system` 抓点查询的事实数据源。

策划的实际诉求有两点：

1. 打点时就能直接设定点的"核心半径"和"最大半径"，让每个动线点自带可抓取语义，而不是打完再逐个到 Inspector 里改。
2. 主动线只勾勒攀爬骨架，真正的抓点分布需要"在动线周围铺一层随机锚点"。手工一个个打太慢，且很难保证锚点都贴在墙面上、间距均匀。

缺少这两项能力，策划要么在 Inspector 里逐点改半径，要么手工撒几十上百个锚点，效率低且容易漏贴墙面。

## What Changes

- 关卡动线编辑器打点时，节点同时挂 `AnchorPoint`，并可在窗口中设置新点的"核心半径"（`previewIntenseRadius`）与"最大半径"（`previewSlightRadius`）。
- 新增"沿动线离散撒点"能力：以当前 `RouteNetwork` 的有向边为骨架，在动线周围随机分布 `AnchorPoint`。
- 撒出的每个锚点 MUST 附着在带 `Wall` 脚本的墙面上（通过射线吸附到墙面命中点，未命中墙面的候选点被丢弃）。
- 提供"点与点之间的离散距离"参数（最小间距），控制撒点疏密；并提供散布带宽、随机种子、探测距离等参数。
- 提供"清空撒点"操作，便于反复调参重撒。
- 非目标：不改动 `MouseFollowJitter`/`level-anchor-system` 的运行时查询逻辑；不做运行时撒点；不导出独立数据文件；撒点只依据编辑期已存在的动线，不自动重算动线。

## Capabilities

### New Capabilities

- `anchor-scatter`: 定义动线打点的锚点半径编辑，以及基于动线与墙面的离散撒点（贴墙约束、离散距离控制、可撤销与随场景保存）要求。

### Modified Capabilities

- 无（`route-editor` 尚未同步至 `openspec/specs/`，相关打点增强在本变更以 `anchor-scatter` 能力描述）。

## Impact

- 影响 `Assets/DesignerSpace/`：修改 `Scripts/Editor/RouteEditorWindow.cs`（新增半径参数、撒点参数与操作）；可能新增撒点算法辅助文件。
- 复用现有 `AnchorPoint.cs`、`Wall.cs`、`RouteNode.cs`，不引入第三方依赖。
- 仅编辑器功能，不产生运行时性能开销；撒点结果以场景 GameObject 持久化，随场景保存并支持 Undo。

## Acceptance Criteria

- 打点模式下可在窗口设置核心半径与最大半径，新打的点带 `AnchorPoint` 且两档半径与设置一致。
- 点击"沿动线离散撒点"后，在动线周围生成一批 `AnchorPoint`，且每个锚点都贴在带 `Wall` 脚本的墙面上。
- 调小"离散距离"参数后再次撒点，锚点分布更密；调大后更稀，且任意两个撒出锚点的间距不小于该距离。
- 撒点与清空撒点操作均可撤销（Ctrl+Z），并标记场景为已修改，保存后重开仍在。
