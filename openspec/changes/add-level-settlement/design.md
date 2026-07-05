## 技术方案

### 结算判定点

在 `ScatterAnchor_*` GameObject 上挂 `SettlementAnchor` 标记组件（与 `AnchorPoint` 同物体）。策划在 Editor 中配置，无需改命名。

### 双入口触发

1. **ControllerMgr**：`FinishHookAndSettle` 锚定成功后，若 `AnchorPoint` 同物体带 `SettlementAnchor`，调用 `LevelSettlement.RequestSettlement`。
2. **ClimbController3D**：`Grab` 成功后检查 `rivet` 对应物体是否带 `SettlementAnchor`（联机运行时 binder 路径）。

两处共享同一 `LevelSettlement` 服务，判定语义与 ControllerMgr 锚定半径一致。

### 计时

`LevelSettlement.BeginSession()` 在 `Climb3CLevelBinder` 绑定完成时调用，使用 `Time.time` 记录起点。`RequestSettlement` 或 `RequestSettlementFromNetwork` 时停止并保存 `ElapsedSeconds`。

### 联机同步

本地触发结算时发送 `game.event`，`eventType=level.finish`，payload 含 `anchorName`、`elapsedSeconds`、`eventId`。远端收到后调用 `RequestSettlementFromNetwork`，不再回发，双方 `LoadScene("GameOver")`。

### GameOver

`GameOverController` 订阅 `VideoPlayer.loopPointReached`，播完后读取 `LevelSettlement.ElapsedSeconds` 显示 uGUI 文本。

## 风险

- MainLevel 当前无 ControllerMgr，单人直玩依赖 Climb3C 桥接。
- GameOver 场景 VideoPlayer clip 需重新绑定现有 mp4。
