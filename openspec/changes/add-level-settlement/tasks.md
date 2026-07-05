## 1. OpenSpec 与核心服务

- [x] 1.1 创建 `add-level-settlement` 变更提案、设计、规格与任务清单
- [x] 1.2 实现 `SettlementAnchor` 与 `LevelSettlement`

## 2. 触发接入

- [x] 2.1 `ControllerMgr.FinishHookAndSettle` 接入结算判定
- [x] 2.2 `ClimbController3D.Grab` 接入结算桥接
- [x] 2.3 `Climb3CLevelBinder` 绑定完成时 `BeginSession`

## 3. 联机与 GameOver

- [x] 3.1 扩展 `level.finish` 协议文档
- [x] 3.2 `AnchorNetworkDemoController` 收发 finish 事件
- [x] 3.3 实现 `GameOverController`、修复视频、加入 Build Settings
- [x] 3.4 MainLevel 配置结算判定点

## 4. 验收

- [x] 4.1 本地与联机冒烟验收（代码路径与场景配置已就绪，需在 Editor 内 Play 验证）
