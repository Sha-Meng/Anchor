# Project Governance Specification

## Purpose

该规格定义 Anchor 项目的基础治理约定，包括中文文档要求和 OpenSpec 规格驱动开发流程。

## Requirements

### Requirement: 中文沟通与文档

项目所有面向用户和协作者的回答、说明、规格、提案、设计文档、任务清单和 README MUST 默认使用简体中文。

#### Scenario: 生成项目文档

- **WHEN** 项目新增或更新 README、OpenSpec 产物或协作说明
- **THEN** 文档内容应使用简体中文

#### Scenario: Agent 回复用户

- **WHEN** Agent 回答用户问题或汇报执行结果
- **THEN** 回复应使用简体中文

### Requirement: OpenSpec 变更治理

项目的重要功能、重构、行为变更或范围不明确的工作 MUST 先通过 OpenSpec 变更提案描述目标、范围、设计和任务，再进入实现。

#### Scenario: 开始重要变更

- **WHEN** 准备新增功能、调整架构、引入技术栈或修改既有行为
- **THEN** 应先在 `openspec/changes/` 下创建对应变更并补齐必要产物

#### Scenario: 完成变更

- **WHEN** 变更实现和验证完成
- **THEN** 应将对应规格同步到 `openspec/specs/` 并归档变更
