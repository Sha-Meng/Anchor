## Why

MainLevel 已有攀爬玩法与 GameOver 场景资源，但缺少通关判定、游玩计时和结算场景流转。双人联机模式下也无法在任意一名玩家抵达终点时同步全员进入结算。

## What Changes

- 在选定的 `ScatterAnchor_*` 上挂 `SettlementAnchor` 标记作为结算判定点。
- `ControllerMgr` 松手锚定成功时检测结算锚点；`ClimbController3D` 抓握成功时做同等桥接（联机路径）。
- 新增 `LevelSettlement` 服务：从关卡玩法就绪开始计时，抵达判定点或收到队友 `level.finish` 时停止并进入 GameOver。
- `GameOver` 场景视频播完后展示本局游玩时长。
- 联机通过 `game.event` / `climb-event.v1` / `level.finish` 同步全员结算。

## Impact

- 新增 `SettlementAnchor`、`LevelSettlement`、`GameOverController`。
- 修改 `ControllerMgr`、`ClimbController3D`、`Climb3CLevelBinder`、`AnchorNetworkDemoController`。
- 更新 `MainLevel.scene`、`GameOver.scene`、`EditorBuildSettings`、网络协议文档。

## 非目标

- 不做排行榜、重试按钮、房间自动解散。
- 不迁移 MainLevel 输入系统到 ControllerMgr（仅补齐结算桥接）。

## Acceptance Criteria

- 本地玩家吸附到带 `SettlementAnchor` 的抓点后立即加载 GameOver。
- GameOver 视频结束后显示 `mm:ss.ff` 格式游玩时长。
- 双人联机：任意一方抵达判定点，双方均进入 GameOver。
- 结算触发幂等，不重复切场景。
- GameOver 已加入 Build Settings。
