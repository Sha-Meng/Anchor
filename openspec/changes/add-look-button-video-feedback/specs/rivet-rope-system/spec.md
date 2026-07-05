## ADDED Requirements

### Requirement: 看向按钮视频反馈

主玩法 UI SHALL 在玩家触发“向上看”和“向下看”按钮时播放对应的 MP4 反馈资源。反馈播放 MUST 不改变按钮原有的按住切换镜头、松开恢复镜头行为。

#### Scenario: 向上看按钮播放反馈

- **WHEN** 玩家按下主玩法 UI 的“向上看”按钮
- **THEN** UI SHALL 切换到向上看镜头姿态
- **AND** UI SHALL 播放“向上看”MP4 反馈

#### Scenario: 向下看按钮播放反馈

- **WHEN** 玩家按下主玩法 UI 的“向下看”按钮
- **THEN** UI SHALL 切换到向下看镜头姿态
- **AND** UI SHALL 播放“向下看”MP4 反馈

#### Scenario: 松开按钮恢复镜头

- **WHEN** 玩家松开“向上看”或“向下看”按钮
- **THEN** UI SHALL 按原有规则恢复到进入看向状态前的镜头姿态
- **AND** 反馈播放逻辑 MUST NOT 阻止镜头恢复
