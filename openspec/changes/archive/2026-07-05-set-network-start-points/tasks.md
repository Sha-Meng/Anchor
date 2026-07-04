## 1. 配置与文档

- [x] 1.1 扩展客户端出生配置结构，支持 `guest` / `host` 的 StartPoint 名称和左右手磁点目标抓点。
- [x] 1.2 更新协议文档和 JSON 配置，写明 `guest` 使用 StartPoint1 + `ScatterAnchor_001/002`，`host` 与单机使用 StartPoint2 + `ScatterAnchor_007/008`。

## 2. 运行时代码

- [x] 2.1 新增 StartPoint MonoBehaviour，用于在 MainLevel 场景中标记 `guest` / `host` 出生点。
- [x] 2.2 改造 MainLevel 联机玩家初始化逻辑，使本地与远端按槽位选择 StartPoint 和左右手目标抓点。
- [x] 2.3 调整单机 MainLevel 默认攀爬绑定，使直接进入场景时按 `host` 位置和左右手抓点初始化。

## 3. 场景接入

- [x] 3.1 在 MainLevel 场景添加 `GuestStartPoint` 与 `HostStartPoint` 两个标记对象，并保留位置供编辑器内手动调整。
- [x] 3.2 确认 MainLevel 中现有 `ScatterAnchor_001/002/007/008` 可被配置引用。

## 4. 验证

- [x] 4.1 检查 C# lints 或编译风险，修复本次变更新增问题。
- [x] 4.2 验证 OpenSpec change 状态与任务完成情况。
