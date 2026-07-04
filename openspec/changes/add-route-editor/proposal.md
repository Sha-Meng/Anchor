## Why

策划需要在关卡墙面上规划角色攀爬的动线（先后经过哪些点）。目前 `DesignerSpace` 只有离散的 `AnchorPoint` 标记和鼠标射线吸附预研，没有一套可以让策划"进入打点模式、在墙面点击打点、并表达点与点先后关系"的工具。缺少这套工具，动线只能靠手工摆放空物体和脑补顺序，效率低且不可视。

## What Changes

- 新增编辑器内关卡动线编辑器：策划打开 EditorWindow 后进入打点模式，在 Scene 视图墙面上点击即可打点。
- 打的点之间用虚线箭头表示先后关系：先打的点指向后打的点；也支持在连线模式下手动连接任意两个已有点（自由图拓扑）。
- 打点只在命中带 `Wall` 脚本的碰撞体时生效，避免误打在地面、人偶、道具等其他碰撞体上。
- 动线数据以场景 GameObject 形式持久化，随场景保存，复用现有 `DesignerSpace.AnchorPoint` 可视化。
- 非目标：不做运行时（Play/打包后）打点；不导出独立 JSON/ScriptableObject 数据文件；不实现动线的运行时寻路/播放逻辑。

## Capabilities

### New Capabilities

- `route-editor`: 定义 Anchor 关卡动线编辑器的打点模式、连线模式、墙面约束、有向动线数据模型和可视化要求。

### Modified Capabilities

- 无。

## Impact

- 影响 `Assets/DesignerSpace/`：新增 `Wall.cs`、`RouteNode.cs` 运行时脚本与 `Scripts/Editor/RouteEditorWindow.cs` 编辑器脚本。
- 修改 `Assets/DesignerSpace/Gym.scene`：给现有 `Wall` 对象挂 `Wall` 脚本；动线节点在编辑时由工具写入场景。
- 仅编辑器功能，不引入运行时性能开销，不新增第三方依赖。
