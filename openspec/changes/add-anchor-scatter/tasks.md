## 1. 打点锚点半径编辑

- [ ] 1.1 在 `RouteEditorWindow` 新增"核心半径 / 最大半径"参数字段（默认值合理）。
- [ ] 1.2 `PlaceNode` 打点时同时 `Undo.AddComponent<AnchorPoint>`，并写入 `previewIntenseRadius`（核心）/ `previewSlightRadius`（最大）。
- [ ] 1.3 窗口提供"将当前半径应用到选中节点"操作，便于批量调整已有点（可选增强）。

## 2. 离散撒点参数与入口

- [ ] 2.1 在 `RouteEditorWindow` 新增撒点参数：离散距离（最小间距）、散布带宽、探测距离、随机种子。
- [ ] 2.2 新增"沿动线离散撒点"操作按钮，读取当前 `RouteNetwork` 下所有 `RouteNode` 边。
- [ ] 2.3 新增"清空撒点"操作按钮，仅删除 `RouteNetwork/ScatterAnchors` 容器。

## 3. 撒点算法

- [ ] 3.1 收集所有 `RouteNode` 有向边为线段集合，按"离散距离/2"步长沿边采样基点，两端法线线性插值得到局部墙面朝向。
- [ ] 3.2 在墙面切平面内按随机方向 / 随机半径（0..散布带宽）生成候选点。
- [ ] 3.3 候选点用"沿法线抬起 + 反向射线"吸附到墙面；仅命中带 `Wall` 的碰撞体才保留，位置沿法线做 `hoverHeight` 偏移。
- [ ] 3.4 用最小间距（离散距离）做泊松盘剔除，保证任意两撒出锚点间距不小于该值。
- [ ] 3.5 用固定随机种子初始化 `System.Random`，保证同参数结果可复现。

## 4. 持久化与生成

- [ ] 4.1 撒出的锚点挂 `AnchorPoint` 并写入核心 / 最大半径，统一放入自动创建的 `RouteNetwork/ScatterAnchors` 容器。
- [ ] 4.2 撒点与清空撒点全部接入 `Undo` 并 `MarkSceneDirty`。

## 5. 校验与验收

- [ ] 5.1 编译校验：`Scripts/Editor/` 为 Editor-only，无编译错误（需在 Unity 编辑器内确认）。
- [ ] 5.2 编辑器内手工冒烟：设置半径打点、沿动线撒点、调离散距离对比疏密、撤销、保存重开验证（需在 Unity 编辑器内完成）。
