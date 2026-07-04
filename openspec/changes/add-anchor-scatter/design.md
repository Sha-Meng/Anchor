## Context

`route-editor` 已实现墙面打点与有向连线，节点挂 `RouteNode`，位置贴在 `Wall` 表面，`transform.forward` 指向墙面外法线。`AnchorPoint` 已定义两档半径（`previewIntenseRadius` 核心、`previewSlightRadius` 最大）供 `MouseFollowJitter` 分档抖动使用。本变更在编辑器窗口内扩展打点与撒点，全程面向策划、可视、可撤销、随场景保存。

## Goals / Non-Goals

**Goals:**

- 打点时节点自带 `AnchorPoint`，核心半径 / 最大半径可在窗口预设。
- 以动线有向边为骨架，在动线周围随机撒 `AnchorPoint`，全部贴在 `Wall` 上。
- 通过"离散距离"（最小间距）控制撒点疏密，结果稳定可复现（随机种子）。
- 撒点、清空撒点接入 Undo 与 `MarkSceneDirty`。

**Non-Goals:**

- 不改运行时抓点查询、抖动逻辑。
- 不做运行时撒点，不导出独立数据文件。
- 不自动生成/重算动线，撒点只消费编辑期已有的 `RouteNode` 边。

## Decisions

### Decision 1: 打点节点同时挂 AnchorPoint 并预设半径

`PlaceNode` 在挂 `RouteNode` 的同时 `Undo.AddComponent<AnchorPoint>`，用窗口的"核心半径 / 最大半径"字段写入 `previewIntenseRadius` / `previewSlightRadius`。窗口字段作为"新点默认值"，已存在的点仍可在 Inspector 单独微调。

理由：策划要求打点即带可抓取语义，核心/最大半径正好对应 `AnchorPoint` 两档半径，直接复用避免新增字段与二次可视化。

备选：在 `RouteNode` 上另加两个半径字段。被否，会与 `AnchorPoint` 语义重复，且 `MouseFollowJitter`/`level-anchor-system` 只认 `AnchorPoint`。

### Decision 2: 撒点以有向边为线段骨架，沿边采样候选

收集 `RouteNetwork` 下所有 `RouteNode` 的有向边 `(a -> b)` 作为线段集合。沿每条线段按步长采样基点，基点法线由两端节点法线线性插值近似；在墙面切平面内按随机方向、随机半径（0..散布带宽）偏移，得到候选点。

理由：动线本身就是攀爬骨架，"在动线周围随机分布"最自然的定义就是沿边的带状区域内撒点。用两端法线插值得到局部墙面朝向，避免依赖硬编码平面。

备选：对整堵墙做面采样。被否，与"基于动线"的诉求不符，且会撒到与动线无关的区域。

### Decision 3: 候选点用射线吸附到 Wall，未命中即丢弃

对每个候选点，以 `candidate + normal * probeDistance` 为起点，沿 `-normal` 方向发射长度 `2 * probeDistance` 的射线；命中碰撞体且 `GetComponentInParent<Wall>() != null` 时，锚点落在 `hit.point + hit.normal * hoverHeight`，朝向 `hit.normal`；否则丢弃该候选。

理由：动线点贴墙但候选点做了切向随机偏移，可能偏离墙面（凹凸、拐角、边界外）。用"沿法线正向抬起再反向打回"的方式，保证最终点精确贴合真实墙面，并且天然过滤掉墙面外或非 `Wall` 区域的候选，满足"必须附着在 wall 上"。

备选：直接把候选点当锚点。被否，无法保证贴墙，且可能穿出墙体或落在非墙面上。

### Decision 4: 用泊松盘式最小间距剔除控制离散距离

维护已接受锚点位置列表（可选纳入场景中已有 `AnchorPoint`）。新候选吸附到墙面后，只有与所有已接受点的距离都 `>= 离散距离` 时才接受。沿边采样步长取"离散距离"的一半，配合每个基点少量随机候选，使点在满足最小间距下尽量铺满。

理由："点与点之间的离散距离"直接映射为最小间距约束，参数直观；泊松盘保证既不过密也不成团。

备选：固定数量随机撒点。被否，密度不可控，且难以保证最小间距。

### Decision 5: 固定随机种子，撒点结果可复现

撒点使用窗口 `散布随机种子` 初始化 `System.Random`，同参数同种子结果一致，便于策划反复对比调参。撒出的锚点统一挂在 `RouteNetwork/ScatterAnchors` 容器下，与手工打点节点分离，"清空撒点"只删该容器。

理由：可复现降低调参心智负担；独立容器让批量重撒不误删手工动线点。

## Risks / Trade-offs

- 拐角/薄墙处法线插值可能不准，导致个别候选吸附失败：可接受，失败候选被丢弃不会产生错误点；可通过增大 `探测距离` 缓解。
- 撒点数量随墙面复杂度增长可能较多：撒点为一次性编辑操作、非每帧逻辑，且受最小间距上限约束，风险可控。
- 已有场景 `Wall` 需确认挂脚本，否则撒点无命中：沿用窗口"为选中对象添加 Wall 脚本"按钮，验收步骤中提示。

## Migration Plan

纯新增编辑器能力，无存量数据迁移。打点半径字段有默认值，不影响既有节点；撒点结果落在新容器，不影响既有动线。
