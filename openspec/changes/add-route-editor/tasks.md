## 1. 墙面标记与约束

- [x] 1.1 新增 `Wall.cs` 标记脚本（`DesignerSpace` 命名空间），作为可打点墙面的标识组件。
- [ ] 1.2 给场景 `Gym.scene` 中现有 `Wall` 对象挂上 `Wall` 脚本（需在编辑器内完成，可用窗口"为选中对象添加 Wall 脚本"按钮）。
- [x] 1.3 打点判定以 `hit.collider.GetComponentInParent<Wall>() != null` 为唯一依据，非墙面点击被忽略。

## 2. 动线数据模型

- [x] 2.1 新增 `RouteNode.cs`（`DesignerSpace` 命名空间），包含有向后继列表 `nextNodes` 与可选 `label`。
- [x] 2.2 实现 `RouteNode.OnDrawGizmos` 对每个后继绘制虚线 + 箭头三角，编辑器内可见。
- [x] 2.3 提供去重与自环过滤的连边辅助方法（`TryAddNext`）。

## 3. 编辑器窗口

- [x] 3.1 新增 `Scripts/Editor/RouteEditorWindow.cs`，菜单入口打开窗口。
- [x] 3.2 提供打点模式、连线模式开关，以及射线 LayerMask、法线偏移、命名前缀等参数。
- [x] 3.3 提供清空当前网络、重置起点、为选中对象加 Wall 等操作按钮。

## 4. Scene 视图交互

- [x] 4.1 打点模式射线打墙面生成节点，并自动在上一个点与新点之间建立有向边。
- [x] 4.2 连线模式点选两个已有节点建立有向边，支持自由图与去重。
- [x] 4.3 非墙面悬停时给出"当前不是可打点墙面"的视觉提示，并吞掉左键点击避免误选。

## 5. 持久化与验收

- [x] 5.1 自动创建 `RouteNetwork/RouteNodes` 层级，节点挂入其下。
- [x] 5.2 打点、连线、清空全部接入 Undo 并 MarkSceneDirty。
- [ ] 5.3 编译校验：`Scripts/Editor/` 为 Editor-only，无编译错误（本地已通过静态复核，需在 Unity 编辑器内确认编译）。
- [ ] 5.4 编辑器内手工冒烟：墙面打点/连线/撤销/保存后重开验证（需在 Unity 编辑器内完成）。
