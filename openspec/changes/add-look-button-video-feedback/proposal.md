## Why

主玩法 UI 已有“向上看”和“向下看”按钮用于临时切换镜头，但按钮缺少对应的语音/视频反馈。已有 `Assets/Art/向上看.m4a` 与 `Assets/Art/向下看.m4a` 音频素材，需要转换为 MP4 并在按钮触发时播放。

## What Changes

- 将“向上看”“向下看”两段音频转换为可由 Unity `VideoPlayer` 播放的 MP4 资源。
- 主玩法 UI 按下“向上看”按钮时，保持现有镜头切换行为，并播放“向上看”MP4。
- 主玩法 UI 按下“向下看”按钮时，保持现有镜头切换行为，并播放“向下看”MP4。

## Impact

- 新增 `Assets/Art/Resources/LookVideos/向上看.mp4` 与 `Assets/Art/Resources/LookVideos/向下看.mp4`。
- 修改 `RivetRopeMainGameplayUi`，运行时创建隐藏 `VideoPlayer` 与 `AudioSource` 播放按钮反馈。

## 非目标

- 不改变看向按钮的按住/松开镜头恢复规则。
- 不显示 MP4 画面，仅使用其中的声音反馈。
- 不新增场景内手工绑定要求。

## Acceptance Criteria

- 点击或按下“向上看”按钮时，镜头切到向上看姿态，并播放“向上看”反馈。
- 点击或按下“向下看”按钮时，镜头切到向下看姿态，并播放“向下看”反馈。
- 松开按钮后仍按现有逻辑恢复原镜头姿态。
