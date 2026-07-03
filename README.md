# Anchor

Anchor 是一个遵循 OpenSpec 规范的 Unity 项目。当前仓库已同步 `E:\AnchorGameJam\AnchorGame` 的 Unity 工程文件；新增功能、框架或重要约定前，应先通过 OpenSpec 变更提案明确目标、范围和验收标准。

## 语言约定

- 所有回答默认使用简体中文。
- 所有项目文档默认使用简体中文。
- OpenSpec 的提案、设计、任务和规格文档都应使用简体中文。

## OpenSpec 工作流

OpenSpec 的核心目录如下：

- `openspec/specs/`：当前系统行为的事实来源。
- `openspec/changes/`：待评审或正在实现的变更。
- `openspec/config.yaml`：项目上下文和文档规则。

推荐流程：

1. 使用 `/opsx:propose "变更说明"` 创建变更提案。
2. 评审并完善 `proposal.md`、`design.md`、`tasks.md` 和规格变更。
3. 使用 `/opsx:apply` 按任务实现。
4. 验证通过后使用 `/opsx:archive` 归档变更并同步规格。

## Unity 项目

Unity 工程位于仓库根目录，关键目录如下：

- `Assets/`：游戏资源、场景和脚本。
- `Packages/`：Unity 包依赖声明。
- `ProjectSettings/`：Unity 项目设置。

当前项目版本信息：

- Unity/Tuanjie Editor：`2022.3.62t11`
- Tuanjie Editor：`1.9.3`

以下目录和文件属于 Unity 本地生成内容，不进入版本控制：

- `Library/`
- `Temp/`
- `Obj/`
- `Logs/`
- `UserSettings/`
- `Build/`、`Builds/`
- 自动生成的 `*.sln`、`*.csproj` 等 IDE 工程文件

## 常用命令

```bash
npm run openspec:doctor
npm run openspec:context
npm run openspec:list
npm run openspec:list:specs
npm run openspec:view
npm run openspec:validate
```

也可以直接使用 OpenSpec CLI：

```bash
openspec doctor
openspec context
openspec list
openspec list --specs
openspec view
openspec validate
```

## Cursor 命令

OpenSpec 初始化时已安装 Cursor slash commands。重启 Cursor 后，可以在聊天框中使用：

```text
/opsx:explore
/opsx:propose "你的变更说明"
/opsx:apply
/opsx:sync
/opsx:archive
```
