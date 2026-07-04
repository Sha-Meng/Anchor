# ClimbGame

本目录包含两套攀爬实现：

- **Climb3C（当前主玩法，3D）** — 见下方"Climb3C（3D 攀爬 3C）"。左右手交替、触点驱动手部 IK、铆钉抓力点、靠近度震动、RenderTexture 放大镜、耐力条与布娃娃摔落，全部参数由 ScriptableObject 配置。脚本在 `Scripts/Climb3C/`。
- **2D 方向摇杆原型（旧，保留参考）** — 见"2D 攀爬 Demo（旧原型）"。与主玩法模型不同，不再作为主入口，仅保留供参考。

---

## Climb3C（3D 攀爬 3C）

### 快速开始

1. 菜单 **Tools ▸ ClimbGame ▸ 3C ▸ Create & Open Demo Scene** 生成并打开 `Scenes/Climb3CDemo.unity`。
   （或新建空物体挂 `Climb3CBootstrap` 组件，直接 Play。相机/灯光/墙/铆钉/角色/UI 全部程序化创建并连线。）
2. 点击 **Play**。
3. 操作：屏幕**左右各一个输入区**，先按下的一侧决定**首个攀爬手**；按住拖动触点（编辑器用**鼠标**）让当前手靠近铆钉，进入吸附半径即完成本次攀爬并**换手**；抓到前松手则**放弃并归位**。触点附近有 RT 放大镜观察着力点，越靠近铆钉手机震动越强。
4. 攀爬时**耐力条**下降，稳定抓住铆钉后快速恢复；耐力归零角色以**布娃娃**摔落一段距离，落定后吸附最近下方铆钉恢复。
5. 菜单 **Tools ▸ ClimbGame ▸ 3C ▸ Create Default Config Assets** 生成 7 个配置 SO 到 `Config/`，拖到 `Climb3CBootstrap` 对应字段即可实时调参。

### 分层（`Scripts/Climb3C/`）

- `Config/` — 7 个 ScriptableObject：ClimbTuning / ArmRig / Haptic / Magnifier / Stamina / RagdollFall / ClimbCamera。
- `Core/` — 手枚举、铆钉 `RivetPoint` / `RivetField`、两骨 `ArmIkSolver`。
- `Input/` — `ClimbTouchInput`（左右输入区、触屏/鼠标）、`WallProjector`（触点射线映射到 3D 墙面）。
- `Feedback/` — `IHapticBackend`/平台后端/`HapticService`（靠近度→震动）、`HandMagnifier`（RT 放大镜）。
- `Character/` — `BodyPart`、`ClimbCharacter`（程序化基本体骨架 + 两骨 IK 驱动 + 自包含 CharacterJoint 布娃娃）。
- `Gameplay/` — `ClimbStamina`、`ClimbController3D`（左右手交替状态机 + 双手中点重心 + 摔落）、`ClimbCamera`。
- `UI/` — `StaminaBarUI`（屏幕耐力条）。
- `Boot/` — `Climb3CBootstrap`（组合根）。

### 说明与后续

- 角色目前是**程序化基本体拼装**（占位美术），布娃娃是**自包含的 Rigidbody + CharacterJoint**，可后续替换为项目内 `FImpossible / Ragdoll Animator 2` 驱动的正式低多边形模型。
- 变强度震动：Android 走振动器 amplitude；iOS 暂用 `Handheld.Vibrate` + 脉冲频率调制回退，后续可接 CoreHaptics 原生插件；编辑器用放大镜边框等可视化回退。
- 所有脚本已用 Tuanjie 2022.3.62t11 离线编译校验通过。

---

## 2D 攀爬 Demo（旧原型）

一个最小可运行的 2D 攀爬玩法：角色在一面墙（2D 平面）上，根据**摇杆方向**自由攀爬；
为了方便在 Editor 里调试，同时支持 **WASD / 方向键**。

所有美术资源（像素风攀爬动画、墙面、摇杆）都是**运行时程序化生成**的，
项目里不需要任何图片文件，导入即可运行。

## 快速开始

1. 打开菜单 **Tools ▸ ClimbGame ▸ Create & Open Demo Scene**
   （会在 `Assets/ClimbGame/Scenes/ClimbDemo.unity` 生成并打开一个演示场景）。
2. 点击 **Play**。
3. 操作：
   - **键盘**：`W/A/S/D` 或方向键，控制上下左右攀爬。
   - **摇杆**：拖动屏幕左下角的虚拟摇杆，角色朝摇杆方向攀爬。

> 备选方式：新建一个空 GameObject，挂上 `ClimbGameBootstrap` 组件，直接 Play 也可以。
> 摄像机 / 墙面 / 摇杆 / 角色 都会自动创建并连线。

菜单 **Tools ▸ ClimbGame ▸ Export Climber Sprites to PNG** 可以把程序化生成的
攀爬帧导出成 PNG 资源（放在 `Assets/ClimbGame/Art/Generated`），方便查看或二次编辑。

## 架构（高内聚 / 低耦合）

各层只依赖接口，互不知道对方的实现细节；`ClimbGameBootstrap` 是唯一的
“组合根（composition root）”，负责把它们拼装并连线起来。

```
Inputs（输入层，与角色无关）
  IClimbInput            —— “玩家想往哪爬” 的抽象（Vector2 方向，模长 0..1）
  KeyboardClimbInput     —— WASD / 方向键，Editor 调试用
  VirtualJoystick        —— uGUI 屏幕摇杆（触屏 + 鼠标）
  CompositeClimbInput    —— 合并多个输入源，谁的意图最强用谁

Character（角色层，与输入设备无关）
  IClimberMotion         —— 角色运动状态的只读视图（给动画层用）
  ClimberController      —— 消费 IClimbInput，在 2D 平面上移动并限制在攀爬区域内
  ClimberAnimator        —— 消费 IClimberMotion，播放翻页式攀爬动画 + 朝向翻转

Art（资源层，纯生成，无外部依赖）
  TextureUtil            —— 像素绘制 / 描边 / 生成 Sprite 的工具
  ClimberSpriteFactory   —— 程序化生成像素风攀爬动画帧
  WallTextureFactory     —— 程序化生成墙面背景

Boot（组合根）
  ClimbGameBootstrap     —— 创建相机/墙/角色/摇杆，并把各层连线起来

Editor（仅编辑器）
  ClimbGameEditor        —— 一键创建演示场景 / 导出 PNG
```

### 为什么这样设计
- **输入可替换**：`ClimberController` 只依赖 `IClimbInput`，加手柄、AI 等新输入源
  不需要改动角色逻辑。
- **动画与移动解耦**：`ClimberAnimator` 只通过 `IClimberMotion` 读取状态，
  不关心移动是怎么算出来的，也完全不知道输入。
- **资源自包含**：美术全部由代码生成，避免了 Sprite 切图、`.meta`、GUID 引用等易碎环节。
- **依赖集中在组合根**：只有 `ClimbGameBootstrap` 同时了解各子系统，其余类保持单一职责。

## 可调参数
在 `ClimbGameBootstrap` 组件上可以调整：攀爬区域大小、移动速度、每单位像素、
动画帧数、背景色、摇杆颜色、以及是否自动创建相机 / 墙 / 摇杆。
