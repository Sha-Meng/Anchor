---
name: tuanjie-implementer
description: 在 Anchor 的 Tuanjie Editor 1.9.3 / Unity 2022.3.62t11 项目中实现玩法和工具。Use when 用户要求实现、落地、写脚本、改场景、接组件、修编译、使用 MCP 操作 Editor 或调整 Unity/Tuanjie 资源。
---

# Tuanjie Implementer

## 技术边界

- 目标编辑器：Tuanjie Editor 1.9.3，`2022.3.62t11`。
- 不使用 Unity 6 专用 API。
- 优先兼容项目现有包：UGUI、TextMeshPro、Timeline、Visual Scripting、Unity Test Framework 1.1.33。
- 不改 `Library/`、`Temp/`、`Obj/`、`Logs/`、`UserSettings/`、`Build/`、`Builds/`。

## 实现流程

1. 先读相关场景、脚本、Prefab 或 OpenSpec 变更，不凭空假设结构。
2. 若是新玩法或行为变化，确认已有 OpenSpec 变更；没有则先建议创建或补充。
3. 拆成最小可玩任务：数据、场景对象、脚本、反馈、验收。
4. 代码优先简单 MonoBehaviour 和 ScriptableObject；game jam 中避免过早抽象。
5. 如 MCP 可用，优先用 MCP 读取场景、Console、截图和测试结果；MCP 不可用时说明阻塞并使用文件级修改。
6. 修改后检查编译/脚本语法，能跑测试就跑测试。

## 编码偏好

- 使用清晰命名和小脚本，避免巨型 Manager。
- 运行时引用优先序列化字段和显式绑定，少用全局查找。
- 只在复杂逻辑前加简短注释。
- 对 jam 原型，先保证可玩闭环，再提炼架构。

## 验收输出

完成实现时说明：

- 改了什么玩家可感知行为。
- 需要在 Editor 内绑定或确认的对象。
- 已运行的验证，以及未能验证的原因。
