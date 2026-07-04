## Context

Anchor 的关卡以墙面（崖壁）为主体，策划需要在墙面上规划角色的攀爬动线：先经过哪个点、再经过哪个点。现有 `DesignerSpace` 已有 `AnchorPoint`（离散锚点 + 半径 Gizmo）和 `MouseFollowJitter`（鼠标射线打墙面得到命中点和法线）的预研，说明"射线打墙面"这套技术路线在本项目可用。

本工具面向策划在 Tuanjie Editor 1.9.3 / Unity 2022.3.62t11 编辑器内使用，目标是低成本、可视、可撤销、随场景保存。

## Goals / Non-Goals

**Goals:**

- 提供一个 EditorWindow，让策划一键进入/退出打点模式。
- 打点模式下在墙面点击生成节点，相邻两点自动生成"先->后"有向虚线箭头。
- 连线模式下可连接任意两个已有节点，形成自由有向图。
- 打点严格限制在带 `Wall` 脚本的碰撞体上。
- 数据以场景 GameObject 持久化，支持 Undo 和场景保存。

**Non-Goals:**

- 不做运行时打点或运行时动线播放。
- 不导出独立数据文件（JSON/ScriptableObject）。
- 不实现节点属性编辑器（如停留时长、事件），仅保留可扩展的 `label` 字段。

## Decisions

### Decision 1: 数据即场景对象，复用 AnchorPoint

每个打的点是一个 GameObject，挂 `AnchorPoint`（复用现有半径可视化）+ 新增 `RouteNode`（存有向后继列表）。整棵树挂在场景根的 `RouteNetwork/RouteNodes` 空物体下，随场景保存。

理由：策划要求数据随场景走、可直接在 Hierarchy 看到、可手工微调位置。复用 `AnchorPoint` 避免重复造可视化。

备选方案：ScriptableObject/JSON 资产。被否，因为脱离场景不便于策划直接在 Scene 视图对照墙面调整。

### Decision 2: 墙面用标记组件 Wall 强校验

打点判定的唯一依据是射线命中的碰撞体能否 `GetComponentInParent<Wall>()`。`LayerMask` 只做粗过滤。

理由：场景里地面、人偶、道具都有碰撞体，仅靠 layer 容易误配置；标记组件语义清晰、策划可控、可扩展（未来可在 `Wall` 上加参数）。

备选方案：仅用 LayerMask。被否，容易误打且需要额外维护 layer 约定。

### Decision 3: 虚线箭头用 Gizmos/Handles 绘制

有向边由 `RouteNode.OnDrawGizmos` 分段绘制虚线 + 箭头三角，编辑器内可见，不生成 LineRenderer 运行时对象。

理由：仅编辑器需要，避免污染运行时场景与 Draw Call。

### Decision 4: 全程接入 Undo 与 MarkSceneDirty

生成节点用 `Undo.RegisterCreatedObjectUndo`，改后继列表用 `Undo.RecordObject`，并 `EditorSceneManager.MarkSceneDirty`。

理由：保证策划可撤销、场景可保存、行为符合 Unity 编辑器直觉。

## Risks / Trade-offs

- Scene 视图点击可能与默认选择/框选冲突：通过 `Event.current.Use()` 在打点/连线模式吞掉左键点击缓解，退出模式后恢复默认。
- 自由图可能产生重复边或自环：连线时做去重与自环过滤。
- 现有 `Wall` 对象需要手工确认脚本已挂：在验收步骤中列出确认项。

## Migration Plan

新增功能，无存量数据迁移。给现有 `Gym.scene` 的 `Wall` 对象补挂 `Wall` 脚本即可启用。
