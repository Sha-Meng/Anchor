# 受力系统接入说明

该目录实现 `define-force-system` 变更的 MVP 版本。核心目标是让受力判定可以在其他玩法系统未完成时先完成单元测试，并在后续角色 3C、关卡和绳索系统完成后快速接入。

## 模块边界

- 关卡系统实现 `IGripQueryProvider`，根据手部位置和查询半径返回 `GripQueryResult`。
- 角色 3C 或临时调试组件实现/使用 `IForceInputAdapter`，把左右手接触、抓点事实、耐力和身体状态组装成 `ForceEvaluationInput`。
- `ForceEvaluator` 只消费输入快照、`ForceEvaluationMemory` 和 `ForceEvaluationSettings`，输出 `ForceEvaluationResult`。
- 绳索系统后续可实现 `IForceFallEventSink`，接收坠落事实；受力系统不计算绳长、伤害或生命变化。

## 抓点事实

`GripQueryResult` 只描述关卡事实，不描述角色最终是否稳定：

- `PointType`：`None`、`ValidHold`、`Obstacle`、`Fake`。
- `GripQuality`：0 到 1 的抓点稳定值。
- `IsFakeRevealed`：假点是否已经暴露。
- `PointId` 和 `DebugName`：用于日志和调试。
- `SurfaceNormal`：后续可用于角色手部贴合表现。

## 稳定值规则

- 低于 `MinGripQuality`：该手无效，原因是 `GripQualityTooLow`。
- 达到 `MinGripQuality` 但低于 `StableGripQuality`：该手可受力但不稳定，结果会标记 `HasUnstableGrip`，并给出建议耐力消耗倍率。
- 达到 `StableGripQuality`：正常稳定受力。
- `Obstacle` 和已暴露 `Fake` 优先于稳定值，始终视为无效受力。

## 测试方式

EditMode 测试位于 `Tests/EditMode/ForceSystemTests.cs`。测试直接构造 `ForceEvaluationInput`，不依赖真实场景、Collider、Rigidbody、角色 3C 或绳索系统。

## 手工验收

可在临时 GameObject 上添加 `ForceSystemDebugDriver`：

1. 创建或指定 `ForceSystemConfig`，调整耐力阈值、抓点稳定值阈值和坠落缓冲时间。
2. 在 `leftHand`、`rightHand` 中手动配置点类型、稳定值、假点状态和耐力。
3. 勾选 `logStateChanges`，运行场景观察稳定、单手脱力、双手脱力和坠落日志。
4. 后续关卡系统可提供实现了 `IGripQueryProvider` 的组件，并把它绑定到 `gripQueryProviderSource`。
