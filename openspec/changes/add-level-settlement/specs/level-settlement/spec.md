## ADDED Requirements

### Requirement: 结算判定点标记

关卡 MUST 允许在 `ScatterAnchor` 上通过 `SettlementAnchor` 组件标记结算判定点。带该标记的锚点 MUST 被识别为通关终点。

#### Scenario: 策划配置判定点

- **WHEN** 策划在 Editor 中给 `ScatterAnchor_160` 挂上 `SettlementAnchor`
- **THEN** 运行时该锚点 MUST 被 `LevelSettlement` 识别为有效结算判定点

### Requirement: 锚定触发结算

当玩家通过 `ControllerMgr` 或 `ClimbController3D` 成功吸附到结算判定点时，系统 MUST 触发关卡结算。

#### Scenario: 单手吸附到判定点

- **WHEN** 玩家松手后小球/手成功锚定到带 `SettlementAnchor` 的锚点
- **THEN** 系统 MUST 停止游玩计时并进入 GameOver 场景

#### Scenario: 重复触发被忽略

- **WHEN** 结算已触发
- **THEN** 后续吸附判定点 MUST NOT 再次触发切场景

### Requirement: 游玩计时

系统 MUST 从 MainLevel 玩法就绪时刻开始计时，在本地触发结算或收到队友 finish 事件时停止。

#### Scenario: 计时起点

- **WHEN** `Climb3CLevelBinder` 完成绑定
- **THEN** `LevelSettlement` MUST 开始计时

#### Scenario: 抵达终点停止

- **WHEN** 本地玩家抵达结算判定点
- **THEN** 计时 MUST 停止并记录 `ElapsedSeconds`

### Requirement: GameOver 展示用时

GameOver 场景 MUST 在结算视频播放结束后展示本局游玩时长。

#### Scenario: 视频结束后显示

- **WHEN** GameOver 场景 VideoPlayer 播放结束
- **THEN** UI MUST 显示格式为 `mm:ss.ff` 的游玩时长

### Requirement: 联机结算同步

双人联机时，任意一名玩家抵达结算判定点 MUST 使房间内所有玩家进入 GameOver。

#### Scenario: 本地玩家先抵达

- **WHEN** 本地玩家触发结算且已连接 relay
- **THEN** 客户端 MUST 发送 `level.finish` 事件并加载 GameOver

#### Scenario: 队友先抵达

- **WHEN** 客户端收到远端 `level.finish` 事件
- **THEN** 客户端 MUST 停止本地计时并加载 GameOver
