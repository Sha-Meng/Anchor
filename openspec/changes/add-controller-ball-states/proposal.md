## Why

`ControllerMgr` 当前只在按住指针时移动离命中点最近的小球，没有小球自身状态、耐力断开、松手锚定结算，也没有释放后的待机点规则。`HandFollowController` 也只有一档磁点跟随速度，无法针对锚定中、释放中、Hook 中调手感。

这会导致玩家控制小球时缺少明确的风险/收益：松手位置、锚点范围和体力消耗不会改变结果，磁点也无法在不同状态下呈现不同响应。

## What Changes

- `ControllerMgr` 为 A/B 球增加三态：锚定中、释放中、Hook 中。
- 小球 Hook 中持续消耗耐力；耐力耗尽时断开控制并进入释放中。
- 玩家松手时按小球和 `AnchorPoint` 的位置关系结算：核心区域直接锚定，外环区域锚定并扣 30% 最大耐力，脱离所有锚点则释放并扣 50% 最大耐力。
- 释放中计算待机位置：取两个小球中点下方的屏幕点，从该屏幕点发射射线，首个有效碰撞点作为小球待机位置。
- `HandFollowController` 支持分别配置锚定中、释放中、Hook 中三种状态的磁点跟随速度。

## Impact

- 修改 `Assets/DesignerSpace/Scripts/Level/ControllerMgr.cs`。
- 修改 `Assets/DesignerSpace/Scripts/Level/HandFollowController.cs`。
- 复用现有 `DesignerSpace.AnchorPoint` 的 `previewIntenseRadius` 作为核心半径、`previewSlightRadius` 作为外环最大半径。
- 不引入第三方依赖，不改 Unity/Tuanjie 生成目录。

## Acceptance Criteria

- Hook 中耐力归零时，小球停止被指针控制并进入释放中。
- 松手时在核心区域内，小球进入锚定中且不扣额外耐力。
- 松手时在外环区域内，小球进入锚定中并扣除 30% 最大耐力。
- 松手时不在任何 `AnchorPoint` 范围内，小球进入释放中并扣除 50% 最大耐力。
- 释放中小球移动到按“两球中点下方屏幕点射线首个碰撞点”计算出的待机位置。
- 手部磁点可按小球三种状态使用不同跟随速度。
