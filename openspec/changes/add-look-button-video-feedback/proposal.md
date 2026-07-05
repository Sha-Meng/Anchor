## Why

主玩法 UI 已有“向上看”和“向下看”按钮用于临时切换镜头，但按钮缺少对应的语音反馈。已有 `Assets/Art/向上看.m4a` 与 `Assets/Art/向下看.m4a` 音频素材，需要转换为 MP3 并在按钮触发时播放。

## What Changes

- 将“向上看”“向下看”两段音频转换为 MP3 资源。
- 主玩法 UI 按下“向上看”按钮时，保持现有镜头切换行为，并播放“向上看”MP3。
- 主玩法 UI 按下“向下看”按钮时，保持现有镜头切换行为，并播放“向下看”MP3。

## Impact

- 新增 `Assets/Art/Resources/LookAudios/向上看.mp3` 与 `Assets/Art/Resources/LookAudios/向下看.mp3`。
- 修改 `RivetRopeMainGameplayUi`，运行时创建 `AudioSource` 播放按钮反馈。

## 非目标

- 不改变看向按钮的按住/松开镜头恢复规则。
- 不新增场景内手工绑定要求。

## Acceptance Criteria

- 点击或按下“向上看”按钮时，镜头切到向上看姿态，并播放“向上看”反馈。
- 点击或按下“向下看”按钮时，镜头切到向下看姿态，并播放“向下看”反馈。
- 松开按钮后仍按现有逻辑恢复原镜头姿态。
