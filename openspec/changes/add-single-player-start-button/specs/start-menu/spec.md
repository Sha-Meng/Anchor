## ADDED Requirements

### Requirement: 单人游戏入口

登录页面 MUST 提供一个文本为 `单人游戏` 的按钮，用于直接进入单人主关卡。

#### Scenario: 点击单人游戏进入 MainLevel2

- **WHEN** 玩家在登录页面点击 `单人游戏`
- **THEN** 系统 MUST 加载 `MainLevel2` 场景

#### Scenario: 构建包可加载单人关卡

- **WHEN** 游戏以构建包运行
- **THEN** `MainLevel2.scene` MUST 已包含在 Build Settings 中
