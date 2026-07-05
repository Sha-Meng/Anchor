## Why

当前 `HandFollowController` 只把手部磁点（`RA2MagnetPoint`）的位置跟随到指针小球，磁力（`DragPower`）恒定为吸附锁定，没有随交互开关。这带来两个问题：

1. 玩家松开小球后手仍被永久钉住，无法表达"抓稳 / 勉强抓住 / 抓空脱手"的差异，也无法惩罚玩家在锚点边缘勉强抓握的行为。
2. 双手小球可以被任意拉开，缺少"两手撑得太开就脱手"的物理约束，攀爬手感不成立。

策划的诉求是：玩家按住小球（hook）时恢复磁力；松手时按小球所处 `AnchorPoint` 区域决定结果（核心=抓稳、外环=勉强抓住并掉耐力、脱离=脱手）；并且两手距离过大时立刻断磁力。

## What Changes

- `HandFollowController` MUST 控制手部磁点的磁力（`DragPower`），而不仅是位置：
  - 玩家按下 / 摁压屏幕 hook 小球时，该侧手磁力恢复（吸附锁定）。
  - 玩家松开对某侧小球的控制时，按该侧小球到最近 `AnchorPoint` 的区域判定：
    - 核心区域（`距离 <= previewIntenseRadius`）：磁力正常（保持吸附）。
    - 外环区域（`previewIntenseRadius < 距离 <= previewSlightRadius`）：磁力正常，但一次性损失最大耐力的 30%。
    - 脱离（`距离 > previewSlightRadius`）：取消磁力（手脱落）。
- `ControllerMgr` MUST 预设"两个小球之间的最大距离"字段，并对外提供两球实时距离与超距查询。
- 两球距离约束：
  - 当某侧处于 hook 状态时，若两球距离超出最大距离的 10%，MUST 立刻断开该拖动侧的磁力。
  - 玩家按下 / 摁压屏幕时，若两球距离已超出最大距离，该侧磁力点 MUST NOT 恢复磁力。
- 区域判定半径 MUST 复用 `AnchorPoint` 已有的 `previewIntenseRadius`（核心）与 `previewSlightRadius`（外环），作为运行时抓握区域判定的事实来源。
- 非目标：不改动 `RA2MagnetPoint` 插件本身；不改动 `MagnetClimberAvatar` 的摔落/复位磁点逻辑；不改动 `ControllerMgr` 的射线拾取与左右分屏规则；不新增网络同步。

## Capabilities

### New Capabilities

- `hand-magnet-control`: 定义手部磁点在 hook / 松手 / 两球超距场景下的磁力开关规则，以及外环勉强抓握的耐力惩罚要求。

### Modified Capabilities

- 无（`HandFollowController` / `ControllerMgr` 尚未同步至 `openspec/specs/`，相关行为在本变更以 `hand-magnet-control` 能力描述）。

## Impact

- 影响 `Assets/DesignerSpace/`：
  - `Scripts/Level/HandFollowController.cs`（新增磁力状态机，控制 `DragPower`，暴露耐力惩罚事件）。
  - `Scripts/Level/ControllerMgr.cs`（新增两球最大距离字段与查询、每侧指针按住状态）。
  - `Scripts/AnchorPoint.cs`（新增 `CoreRadius` / `OuterRadius` 只读属性，正名两档半径为运行时判定来源）。
  - `Scripts/Anchor.DesignerSpace.asmdef`（引用 `AD_FimpAnimating` 以访问 `RA2MagnetPoint`）。
- 影响 `Assets/ClimbGame/`：
  - `Scripts/Climb3C/Gameplay/ClimbStamina.cs`（新增按比例扣减接口）。
  - `Scripts/Climb3C/Gameplay/ClimbController3D.cs`（订阅耐力惩罚事件并扣耐力）。
- 复用现有 `RA2MagnetPoint`、`AnchorPoint`，不引入第三方依赖。仅运行时逻辑，无网络协议变更。

## Acceptance Criteria

- 玩家按住某侧小球时，该侧手磁力恢复（手被吸附到磁点）。
- 玩家松手时，小球位于核心区域则手保持吸附；位于外环区域则手保持吸附且耐力条一次性下降 30%；脱离锚点则手脱落。
- `ControllerMgr` 可在 Inspector 配置两球最大距离；两球被拉开到超出该距离时，当前 hook 侧手立刻脱落。
- 玩家在两球已超距时按下屏幕，该侧手不会重新吸附。
- Tuanjie 编译通过，`MainLevel2` 场景手动验证上述四类分支表现符合预期。
