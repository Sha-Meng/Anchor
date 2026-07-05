## 1. 程序集与半径来源

- [ ] 1.1 `Anchor.DesignerSpace.asmdef` 的 `references` 增加 `"AD_FimpAnimating"`，使 DesignerSpace 可访问 `RA2MagnetPoint`。
- [ ] 1.2 `AnchorPoint` 新增只读属性 `CoreRadius`（= `previewIntenseRadius`）与 `OuterRadius`（= `previewSlightRadius`），并更新注释，明确两档半径同时是运行时抓握区域判定的事实来源。

## 2. ControllerMgr：两球距离与按住状态

- [ ] 2.1 新增字段 `maxBallDistance`（两球最大距离，默认 2，Inspector 可配），并在 `OnValidate` 做非负规整。
- [ ] 2.2 新增查询：`MaxBallDistance`、`BallDistance`（A/B 球世界距离）、`IsOverMaxDistance(float factor)`。
- [ ] 2.3 新增每侧"指针按住"状态 `IsAHeld` / `IsBHeld`，在 `Update` 遍历指针时按 `screenSplit` 记录（不依赖射线是否命中），与 `IsAActive` / `IsBActive` 区分。

## 3. HandFollowController：磁力状态机

- [ ] 3.1 磁点解析扩展：在拿磁点 `Transform` 的同时缓存其 `RA2MagnetPoint` 组件，用于写 `DragPower`；位置跟随逻辑保留。
- [ ] 3.2 新增可配置字段：`staminaPenaltyFraction`(0.3)、`magnetDragPowerOn`(2)、`magnetDragPowerOff`(0)、`hookOverDistanceFactor`(1.0)、`hookBreakDistanceFactor`(1.1)。
- [ ] 3.3 对左右手各自做 held 边沿检测（上一帧 vs 本帧）：上升沿=hook 按下，下降沿=松手。
- [ ] 3.4 hook 按下：`IsOverMaxDistance(hookOverDistanceFactor)` 为真则 `DragPower = Off`（不恢复），否则 `DragPower = On`。
- [ ] 3.5 hook 持续：`IsOverMaxDistance(hookBreakDistanceFactor)` 为真则立刻 `DragPower = Off`（只断当前拖动侧）。
- [ ] 3.6 松手：遍历 `AnchorPoint` 求最近锚点距离并分档——核心 `On`、外环 `On` 且 `StaminaPenaltyRequested?.Invoke(staminaPenaltyFraction)`、脱离 `Off`。
- [ ] 3.7 新增 `event System.Action<float> StaminaPenaltyRequested`，更新类注释为"控制磁点位置与磁力 DragPower"。

## 4. 耐力接入

- [ ] 4.1 `ClimbStamina` 新增 `DrainFraction(float fraction)`，按 `MaxStamina * fraction` 扣减并 `Max(0, ...)`。
- [ ] 4.2 `ClimbController3D` 在 `Initialize` 末尾查找场景 `HandFollowController` 并订阅 `StaminaPenaltyRequested`，回调 `DrainFraction`；`OnDestroy` 退订。

## 5. 校验与验收

- [ ] 5.1 Tuanjie 编译通过，无编译错误（需在编辑器内确认）。
- [ ] 5.2 `MainLevel2` 手动验证：hook 恢复磁力；松手核心保持、外环保持且耐力 -30%、脱离脱手；按下超距不恢复；hook 中超 10% 断当前拖动侧（需在编辑器内完成）。
