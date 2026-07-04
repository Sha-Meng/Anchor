## Why

当前铆钉绳索表现仍偏向硬性线条连接，玩家难以从画面上感知绳索的重量、松弛、拉紧和材质属性。绳索已经成为攀爬协作玩法的核心反馈通道，现在需要把逻辑绳路升级为更可信、可调、可验收的物理和视觉表现。

## What Changes

- 增强现有铆钉绳索表现，使松弛、拉紧、端点移动和路径转折具备更自然的动态响应。
- 引入绳索材质表现要求，包括宽度、颜色/纹理、法线或等价表面细节、端点/铆钉连接处的视觉过渡。
- 增加可配置的表现模式：轻量 `LineRenderer`/程序化点模拟作为默认方案，可选物理绳段或关节链方案用于更强物理反馈。
- 重新设计专用绳索视觉测试场景，用于直观查看绳索材质、松弛/拉紧、端点跟随、铆钉转折和表现模式切换。
- 对项目内 FImpossible Creations 的 Rope Swing / ragdoll 挂点示例进行参考评估，但不把第三方 demo 代码直接作为玩法规则依赖。
- 明确表现层的物理模拟不得改变铆钉库存、绳长判定、坠落保护、伤害结算和联网同步事实。

## Capabilities

### New Capabilities

- 无

### Modified Capabilities

- `rivet-rope-system`: 增加绳索物理表现、材质表现、可选物理方案和验收场景相关要求。

## Impact

- 影响 `Assets/Anchor/RivetRopeSystem/Runtime` 中绳索视觉、配置和调试组件。
- 可能影响 `Assets/Anchor/RivetRopeSystem/Editor` 中主玩法安装器和验证场景构建逻辑，用于创建材质、预设和演示对象。
- 可能新增或调整绳索材质、贴图、Prefab、ScriptableObject 配置和专用绳索视觉测试场景资源。
- 不改变联网协议的铆钉事件语义，不改变 `rivet.place` / `rivet.collect` 的同步 payload。
- 不引入新的运行时框架；如复用 FImpossible 示例，仅作为局部表现或调研依据，需保持与核心规则解耦。
