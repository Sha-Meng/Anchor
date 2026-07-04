## Context

MainLevel 联机开局已经通过 `hostId`、`players` 和本地 `_isHost` 推导 `host` / `guest` 槽位，但运行时代码仍把本地和远端都放到同一个起攀主抓点附近。现有 `Climb3CLevelBinder` 支持显式左右手起始抓点，并在 `MagnetClimberAvatar.CommitClimbRagdoll()` 中创建 `LeftHandMagnet` / `RightHandMagnet`，因此可以在不改服务器的前提下，把出生点和左右手磁点目标配置到客户端。

## Goals / Non-Goals

**Goals:**

- 双人联机时 `guest` 使用 StartPoint1 与 `ScatterAnchor_001/002`，`host` 使用 StartPoint2 与 `ScatterAnchor_007/008`。
- 单机直接打开 MainLevel 时按 `host` 规则初始化，方便默认演示和关卡调试。
- 通过场景中的 `StartPoint` MonoBehaviour 明确标记出生点，让关卡设计可以直接移动 GameObject 调整身体初始位置。
- 保持左右手初始吸附通过 `LeftHandMagnet` / `RightHandMagnet` 完成，抓点名只作为磁点目标配置。

**Non-Goals:**

- 不修改 relay 服务器、房间状态机或 `room.*` 协议。
- 不新增角色选择、换边 UI 或昵称流程。
- 不改变攀爬输入、耐力、受力或绳索规则。

## Decisions

- 使用场景 StartPoint 标记身体出生位置，而不是继续由两抓点中点自动推导。这样能满足“出生点直接坐在场景中两个 GameObject”的需求，也便于关卡设计手动摆位。
- 使用显式左右手抓点配置，而不是 `useNearestStartAnchorPair`。`guest` 与 `host` 的左右手目标已由设计确定，显式配置可以避免最近邻选择因场景调整而漂移。
- 扩展 `spawnAnchors` 为槽位配置，保留旧字段读取回退。这样能降低旧配置文件缺字段时的风险，同时新默认值直接表达 StartPoint 与左右手磁点目标。
- 单机场景按 `host` 规则处理。这样直接运行 MainLevel 时和房主视角一致，不需要模拟房间身份。
- 远端初始姿态只用于收到第一帧网络状态前的占位表现；后续仍由 `game.state` 驱动，不参与本机输入或玩法判定。

## Risks / Trade-offs

- [StartPoint 未摆到最终位置] → 脚本只负责识别和使用 Transform，关卡设计可在场景中手动移动；验收时检查两个对象是否存在且位置合理。
- [旧 JSON 配置缺少新字段] → 运行时代码提供默认 `guest` / `host` 槽位配置，并保留旧主抓点字段作为回退。
- [同名 `LeftHandMagnet` / `RightHandMagnet` 在本地与远端同时存在] → 本地可控角色继续由 `Climb3CLevelBinder` 创建磁点；远端只读表现不依赖 RA2 磁点控制，避免全局同名查找驱动远端。
- [场景文本 YAML 手工修改易出错] → 只新增简单空对象和脚本引用，代码变更后通过 lints/编译检查降低风险。

## Migration Plan

1. 更新客户端协议配置默认值和文档。
2. 新增 StartPoint 标记脚本。
3. 改造 MainLevel 联机初始化逻辑，并调整单人 binder 默认配置。
4. 在 MainLevel 添加 `GuestStartPoint` 与 `HostStartPoint`，后续可在编辑器中手动摆放位置。
5. 如出现异常，可回退配置到旧抓点并移除 StartPoint 场景对象；服务器无需回滚。
