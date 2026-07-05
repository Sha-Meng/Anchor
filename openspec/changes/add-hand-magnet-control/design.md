## Context

`ControllerMgr` 在场景中生成 A / B 双小球，玩家在屏幕左半边按下驱动 A 球、右半边驱动 B 球（射线拾取到墙面命中点）。`HandFollowController` 每帧把 `LeftHandMagnet` / `RightHandMagnet` 磁点位置平滑跟随到对应小球。磁点是 `MagnetClimberAvatar` 运行时创建的 `RA2MagnetPoint`，其 `DragPower` 控制手骨吸附强度：`2` 且 `KinematicOnMax` 时手被运动学锁定（吸附），`0` 时释放（手随布娃娃下落）。`AnchorPoint` 已定义 `previewIntenseRadius`（核心）与 `previewSlightRadius`（外环）两档半径，作为 `MouseFollowJitter` / `route-editor` / 撒点的事实数据源。耐力由 `ClimbController3D` 持有的 `ClimbStamina` 管理，写入 `ClimberRuntimeState`（单一数据源，便于联机同步）。

本变更在既有"位置跟随"之上，加入"磁力开关"这一新维度，并由两球距离约束保护攀爬手感。

## Goals / Non-Goals

**Goals:**

- hook（按住小球）时恢复该侧磁力；松手时按小球所处 `AnchorPoint` 区域决定磁力与耐力惩罚。
- `ControllerMgr` 预设两球最大距离；hook 期间超出 10% 断当前拖动侧磁力；按下时若已超距则不恢复磁力。
- 区域判定复用 `AnchorPoint` 现有两档半径，避免新增重复配置。

**Non-Goals:**

- 不改 `RA2MagnetPoint` 插件、不改 `MagnetClimberAvatar` 的摔落/复位磁点逻辑。
- 不改 `ControllerMgr` 的分屏与射线拾取规则。
- 不做网络同步（磁力状态是本地表现，耐力仍走既有 `ClimberRuntimeState`）。

## Decisions

### Decision 1: 磁力 = `RA2MagnetPoint.DragPower`，由 `HandFollowController` 边沿驱动写入

`DragPower = magnetDragPowerOn`(默认 2) 表示恢复磁力（吸附锁定），`DragPower = magnetDragPowerOff`(默认 0) 表示取消磁力（脱手）。`HandFollowController` 在解析磁点 `Transform` 的同时缓存其 `RA2MagnetPoint` 组件，用于写 `DragPower`。

写入采用"边沿 + 条件"驱动而非每帧无条件刷新：仅在 hook 上升沿（按下）、hook 持续期超距、松手下降沿这三类时机写 `DragPower`，平时不覆盖。这样与 `MagnetClimberAvatar` 摔落/复位对 `DragPower` 的写入不产生每帧争抢。

理由：`RA2MagnetPoint` 已把 `DragPower` 设计为吸附强度的唯一入口，直接复用最简单、零插件改动。

备选：新增独立"是否吸附"布尔并改插件。被否，改插件成本高且破坏第三方资源可升级性。

### Decision 2: hook / 松手用"指针按住"边沿判定，与"命中驱动"区分

`ControllerMgr` 现有 `IsAActive` / `IsBActive` 表示"本帧该侧指针在对应半屏且射线命中场景并驱动了小球"。但射线可能未命中（指向空白），此时 `IsActive=false` 却不代表玩家松手。为准确判定 hook / 松手，`ControllerMgr` 新增 `IsAHeld` / `IsBHeld`：只要该侧半屏有指针按住即为 true，不依赖射线是否命中。

`HandFollowController` 每侧维护上一帧 held 值，比较得到上升沿（hook 按下）与下降沿（松手）。位置跟随仍沿用 `IsAActive` / `IsBActive`（只有命中时才移动磁点位置，避免把磁点甩到无效点）。

理由：hook / 松手是"玩家是否在按压小球"的语义，应由按住状态决定；磁点位置跟随是"有没有有效目标"的语义，应由命中状态决定。二者分离避免误判。

### Decision 3: 松手区域判定取小球到最近 `AnchorPoint` 的三维距离

松手瞬间，遍历场景 `AnchorPoint`，取该侧小球到最近锚点的三维距离 `d` 与该最近锚点的 `CoreRadius` / `OuterRadius`：

- `d <= CoreRadius` → 核心：`DragPower = On`。
- `CoreRadius < d <= OuterRadius` → 外环：`DragPower = On` 且触发一次耐力惩罚。
- `d > OuterRadius` → 脱离：`DragPower = Off`。

半径按"最近锚点各自的值"取（每锚点可不同），与 `MouseFollowJitter` 的分档口径一致。

理由：复用既有半径字段与"最近锚点"口径，语义统一、无需新增配置；`add-anchor-scatter` 已把这两档半径明确为事实数据源。

备选：在 `ControllerMgr` / `HandFollowController` 配全局半径。被否，会与锚点各自半径冲突，且撒点已支持逐锚点半径。

### Decision 4: 两球最大距离放在 `ControllerMgr`，两个阈值因子在 `HandFollowController`

`ControllerMgr` 新增 `maxBallDistance`（默认 2）作为"两个小球之间的最大距离"的事实来源，并提供 `BallDistance`、`MaxBallDistance`、`IsOverMaxDistance(factor)`。`HandFollowController` 用两个因子消费它：

- 按下 hook 时用 `hookOverDistanceFactor`(默认 1.0)：`IsOverMaxDistance(1.0)` 为真则该侧不恢复磁力。
- hook 持续期用 `hookBreakDistanceFactor`(默认 1.1)：`IsOverMaxDistance(1.1)` 为真则立刻断当前拖动侧磁力。

超距断磁力只作用于"当前正在被按压 / 拖动（该侧 held）"的手，已松手锚定的另一只手不受影响。

理由：距离数据属于双球管理者 `ControllerMgr`；触发因子属于交互规则，放在 `HandFollowController` 便于统一调参。仅断拖动侧符合"是这只手把自己拉脱了"的直觉。

### Decision 5: 耐力惩罚跨程序集用事件解耦

`HandFollowController` 在 `Anchor.DesignerSpace` 程序集，`ClimbStamina` / `ClimbController3D` 在默认 `Assembly-CSharp`。为不让 DesignerSpace 反向依赖 ClimbGame，`HandFollowController` 暴露 `event System.Action<float> StaminaPenaltyRequested`，外环松手时 `Invoke(staminaPenaltyFraction)`（默认 0.3）。`ClimbController3D` 在 `Initialize` 时查找场景 `HandFollowController` 并订阅，回调 `ClimbStamina.DrainFraction(fraction)` 扣减，`OnDestroy` 退订。

为让 DesignerSpace 能访问 `RA2MagnetPoint`，`Anchor.DesignerSpace.asmdef` 的 `references` 增加 `AD_FimpAnimating`（`RA2MagnetPoint` 所在程序集）。`Assembly-CSharp` 默认自动引用 `Anchor.DesignerSpace`（`autoReferenced: true`），故订阅方向可行。

理由：事件解耦保持程序集单向依赖（Assembly-CSharp → DesignerSpace），耐力扣减仍内聚在耐力持有者，符合"单一数据源"。若场景没有 `ClimbController3D`，磁力逻辑照常工作，仅耐力惩罚静默跳过。

备选：DesignerSpace 直接引用 ClimbGame 扣耐力。被否，会形成不必要的反向依赖，且 ClimbGame 无 asmdef 不便被引用。

## Risks / Trade-offs

- 磁力控制权与摔落逻辑重叠：`MagnetClimberAvatar.EnterRagdoll`(=0) / `SetupClimbPose`(=2) 也会写 `DragPower`。本变更为边沿/条件驱动、非每帧刷新，正常攀爬不覆盖；进入 `Falling` 由攀爬控制器主导，磁力控制以攀爬状态为优先。风险可控。
- 松手瞬间遍历 `AnchorPoint`：仅在松手这一帧发生，非每帧全量遍历，开销可忽略；沿用 `FindObjectsOfType` 缓存策略即可。
- 场景缺少 `ClimbController3D` 时耐力惩罚不生效：设计上允许（订阅侧静默跳过），不影响磁力主逻辑。

## Migration Plan

纯新增运行时行为，无存量数据迁移。新增字段均有默认值，既有场景无需改配置即可运行；`AnchorPoint` 两档半径沿用原值，`ControllerMgr.maxBallDistance` 取默认 2。
