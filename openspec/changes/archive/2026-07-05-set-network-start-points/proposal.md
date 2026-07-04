## Why

当前 MainLevel 联机开局仍以单个起攀主抓点推导双方位置，无法表达“非房主在初始点 1、房主在初始点 2”以及两端固定左右手初始吸附目标的设计要求。需要把出生点和左右手磁点目标从“自动临近抓点推导”细化为可在场景中明确摆放和可配置的规则。

## What Changes

- MainLevel 提供两个可识别的 StartPoint 场景对象：`guest` 使用初始点 1，`host` 使用初始点 2。
- 双人联机进入 MainLevel 时，房主固定按 `host` 槽位出生，非房主固定按 `guest` 槽位出生，且两端看到的身份与位置一致。
- 本地攀爬角色的 `LeftHandMagnet` / `RightHandMagnet` 初始吸附目标改为槽位配置的左右手抓点：`guest` 使用 `ScatterAnchor_001/002`，`host` 使用 `ScatterAnchor_007/008`。
- 单机直接进入 MainLevel 时按房主/`host` 规则初始化，方便关卡调试和默认演示。
- 更新客户端 JSON 协议配置和协议文档，移除“双人使用同一主抓点”的旧说明。

## Capabilities

### New Capabilities

- 无。

### Modified Capabilities

- `coop-networking`: 细化 MainLevel 房间槽位、场景 StartPoint、单机默认身份和左右手磁点初始吸附目标的要求。

## Impact

- 影响 `AnchorProtocolConfig` 的出生配置结构和默认值。
- 影响 `AnchorNetworkDemoController` 的 MainLevel 本地/远端玩家初始化逻辑。
- 影响 MainLevel 场景中的 StartPoint 标记对象和单人 `Climb3CLevelBinder` 默认配置。
- 影响 `docs/coop-network-protocol.md`、`docs/coop-network-protocol.config.json` 和 `Assets/StreamingAssets/coop-network-protocol.config.json`。
- 不需要修改 WebSocket relay 服务器；房间身份仍由现有 `hostId` / `players` 信息决定。
