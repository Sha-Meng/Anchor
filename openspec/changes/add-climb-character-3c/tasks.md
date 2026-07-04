> 实现说明：脚本位于 `Assets/ClimbGame/Scripts/Climb3C/`，已用 Tuanjie 2022.3.62t11 的 mcs 离线编译通过（0 error / 0 warning）。角色为程序化基本体拼装 + 自包含 CharacterJoint 布娃娃（后续可替换为 Ragdoll Animator 2 与正式美术）。真机验收与正式美术仍待完成。

## 1. 配置体系（ScriptableObject）

- [x] 1.1 定义 `ClimbTuningConfig`：起攀点、首手规则、伸手/回收 lerp、触点灵敏度与偏移、输入区分割/内边距、躯干重心偏移与平滑
- [x] 1.2 定义 `ArmRigConfig`：上臂/前臂骨长、肩摆范围、肘弯朝向、可达半径夹取
- [x] 1.3 定义 `HapticConfig`：intenseRadius/slightRadius/snapRadius、各档强度/频率、抓握强脉冲、平台策略
- [x] 1.4 定义 `MagnifierConfig`：倍率、半径、偏移、边框反馈、RT 分辨率
- [x] 1.5 定义 `StaminaConfig`：下降速率、恢复速率、耗尽阈值、fallDistance、落地恢复比例
- [x] 1.6 定义 `RagdollFallConfig`：物理混合/冲击/初速、恢复时间、落点规则
- [x] 1.7 定义 `ClimbCameraConfig`：偏移、俯仰、阻尼、摔落跟随
- [x] 1.8 全字段补简体中文 Tooltip；提供 Editor 一键生成默认配置资产菜单

## 2. 3D 场景与墙面凸起

- [x] 2.1 组合根程序化搭建 3D 墙体（Cube）
- [x] 2.2 实现 `RivetPoint`（立体抓点 + 碰撞体 + snapRadius + Gizmo）与错落布点
- [x] 2.3 主相机正对墙面，Canvas 适配竖屏
- [ ] 2.4 用法线/高度贴图强化墙面凸起浮雕（当前为纯色 + 立体铆钉，视觉可继续加强）

## 3. 输入区与 3D 触点映射

- [x] 3.1 实现 `ClimbTouchInput`：左右输入区（可配分割/内边距/裁剪），支持多点触屏
- [x] 3.2 编辑器/PC 鼠标模拟触点回退
- [x] 3.3 输入门控（仅当前攀爬手同侧生效）与首手规则（先触发侧决定首手）
- [x] 3.4 `WallProjector` 触点 → 墙面射线求交映射，单次攀爬只跟踪一个 fingerId
- [x] 3.5 触点/输入区可视化调试（放大镜 + 状态）

## 4. 手部 3D 两骨 IK

- [x] 4.1 实现 `ArmIkSolver`：肩→上臂→肘→前臂→手掌链，手掌触点为末端
- [x] 4.2 余弦定理解算肘，按 `elbowBendDir` 固定弯向
- [x] 4.3 肩关节受约束小幅移动（shoulderSwayRange）
- [x] 4.4 超出可达半径 reachClamp，末端平滑，防拉断/反关节
- [x] 4.5 输出关节位姿驱动基本体骨骼

## 5. 铆钉靠近度与触觉反馈

- [x] 5.1 实现 `RivetField`（最近铆钉/距离查询，3D 距离，含向下查询）
- [x] 5.2 靠近度 → 分档 + 连续强度映射
- [x] 5.3 定义 `IHapticBackend` 抽象层与 `HapticService`
- [x] 5.4 Android 变强度震动（振动器 amplitude, API 26+）
- [ ] 5.5 iOS 原生 CoreHaptics（当前先用 Handheld.Vibrate + 脉冲频率调制回退，后续接原生插件）
- [x] 5.6 能力不足退化为脉冲频率调制；编辑器/PC 可视化回退
- [x] 5.7 仅对当前攀爬手计算靠近度与反馈

## 6. RenderTexture 放大镜

- [x] 6.1 实现 `HandMagnifier`：副相机 → RenderTexture → 圆形遮罩显示在触点附近
- [x] 6.2 接入 MagnifierConfig（倍率/半径/偏移/边框随档位变色/RT 分辨率）
- [x] 6.3 伸手时显示、松手或完成后收起；按需渲染

## 7. 攀爬状态机、躯干重心与组合根

- [x] 7.1 实现 `ClimbController3D` 状态机：WaitingForPress → Reaching → Grabbed / Returning → Falling
- [x] 7.2 Grabbed：强反馈、吸附铆钉、判定完成、换手
- [x] 7.3 Returning：抓握前松手放弃，手平滑回默认/上一抓点，不换手
- [x] 7.4 起攀初始化（中心 + 双手默认位 + 首手待定）
- [x] 7.5 躯干重心跟随双手中点（含偏移/平滑），带动肩部
- [x] 7.6 组合根 `Climb3CBootstrap`：装配相机/墙/铆钉/角色/输入区/放大镜/耐力/布娃娃并引用各 SO
- [x] 7.7 演示场景 Editor 一键创建入口（Tools ▸ ClimbGame ▸ 3C）

## 8. 耐力系统

- [x] 8.1 实现 `ClimbStamina`：伸手/悬挂下降、稳定抓握快速恢复
- [x] 8.2 归零触发摔落流程与落定恢复（重置耐力到配置比例）
- [x] 8.3 屏幕耐力条 UI `StaminaBarUI`（可配显隐）

## 9. 布娃娃摔落

- [x] 9.1 角色接入自包含布娃娃（Rigidbody + CharacterJoint），攀爬时运动学、摔落时全物理
- [x] 9.2 耐力归零：复位静止姿态后开物理、施加向下 + 离墙初速与随机扰动
- [x] 9.3 落定恢复：固定下落距离/超时后吸附最近下方铆钉并回到可攀爬状态
- [x] 9.4 布娃娃参数经 RagdollFallConfig 暴露
- [ ] 9.5 （可选）替换为 Ragdoll Animator 2 驱动的正式模型

## 10. 3C 相机

- [x] 10.1 实现 `ClimbCamera`：跟随躯干重心，偏移/俯仰/阻尼
- [x] 10.2 摔落时相机拉远；参数经 ClimbCameraConfig 配置

## 11. 3D 角色与场景资产

- [x] 11.1 程序化基本体角色骨架（躯干/头/双臂/双腿），支持 IK 与布娃娃
- [x] 11.2 校准骨骼使 IK 驱动与布娃娃切换无错位（关节锚点复位）
- [ ] 11.3 制作正式美术（低多边形角色、带凸起墙体贴图、铆钉、UI 风格）
- [ ] 11.4 用正式资产替换程序化占位并校验

## 12. 验收与收尾

- [ ] 12.1 手工验收：中心双手默认位起攀，先触发侧成首手，左右交替连续 ≥3 次攀爬无报错
- [ ] 12.2 验收门控/3D 映射/放大镜/靠近震动分档/抓即换手/松手归位/双手中点重心
- [ ] 12.3 验收耐力下降/抓点恢复/归零布娃娃摔落/落定恢复
- [ ] 12.4 Android 与 iOS 真机验证变强度震动分档
- [ ] 12.5 运行时调 SO 参数验证即时生效项与需重建项
- [x] 12.6 更新 `Assets/ClimbGame/README.md`，说明 3D 攀爬 3C 与旧 2D 原型关系
- [x] 12.7 脚本用 Tuanjie 2022.3.62t11 mcs 离线编译校验（0 error / 0 warning）
