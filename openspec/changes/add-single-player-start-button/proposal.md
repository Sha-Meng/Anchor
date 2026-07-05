## Why

登录页面目前只有进入联网入口的开始按钮，玩家无法从首页直接进入单人关卡进行快速游玩或调试。

## What Changes

- 在 `Start` 登录场景新增 `单人游戏` 按钮。
- 点击 `单人游戏` 后直接加载 `MainLevel2` 场景。
- 将 `MainLevel2.scene` 加入 Build Settings，保证构建包内可直接加载。

## Impact

- 修改 `Assets/Scenes/Start.scene`。
- 修改 `ProjectSettings/EditorBuildSettings.asset`。

## 非目标

- 不改变现有开始按钮进入联网入口的行为。
- 不新增角色选择、存档或难度选择流程。

## Acceptance Criteria

- 登录页面显示一个文本为 `单人游戏` 的可点击按钮。
- 点击该按钮后加载 `MainLevel2` 场景。
- `MainLevel2.scene` 已包含在构建场景列表中。
