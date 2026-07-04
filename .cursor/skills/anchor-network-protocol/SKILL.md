---
name: anchor-network-protocol
description: 维护 Anchor 双人合作联网协议、WebSocket relay 房间流程、玩家身份规则、payload 扩展模型和部署指引。Use when 编辑网络协议文档、房间消息、Unity/Tuanjie 协议配置、relay 服务行为或合作联网任务。
---

# Anchor Network Protocol

## 快速开始

Use this skill when working on Anchor 的双人合作联网 MVP、WebSocket relay、房间列表、创建/加入房间、房主开始、玩家 ID、协议 payload 扩展或腾讯云部署。

主要参考：

- `docs/coop-network-protocol.md`

OpenSpec 变更：

- `openspec/changes/design-coop-network-layer/`

## 核心规则

- MVP 不做创建角色流程，也不做昵称设置交互。
- 服务器连接成功后分配临时 `playerId`。
- 客户端可生成并本地保存 `clientId`，但不要读取或上传平台设备 ID。
- 服务器只固定理解 `system.*` 和 `room.*`。
- `game.*` 消息由服务器做基础校验后原样转发。
- 攀岩玩法同步字段放在 `payload`，并通过 JSON 协议配置维护。
- 修改 `game.*` JSON payload 配置、schema 示例、发送频率或调试显示名，不需要重新部署服务器。
- 协议字段变更时，同步更新 `docs/coop-network-protocol.md` 和 `docs/coop-network-protocol.config.json`。
- 本次 MVP 验收必须覆盖 demo 场景端到端流程：连接、建房、加入房间、房主开始、进入游戏、`game.state` 和自定义 `game.event` 收发。
- MVP 内测可以使用明文 `ws://`；承载账号、支付、正式 token 或隐私数据前必须改为 `wss://`。

## 工作流程

1. 先阅读 `docs/coop-network-protocol.md`。
2. 判断变更属于哪类：
   - `game.*` payload 扩展：更新 JSON 协议配置和协议文档，通常不改服务器。
   - `room.*` 或 `system.*` 控制流程：更新协议文档、OpenSpec 规格和 relay 服务。
   - 部署或端口变化：更新协议文档的部署指引和任务清单。
3. 如果变更影响需求或行为，先更新当前 OpenSpec change。
4. 实现时保持服务器 relay 简单：连接、房间、基础校验、转发、日志。
5. 验证两个客户端能完成房间列表、创建房间、加入房间、房主开始、进入 demo 场景、`game.state` 转发和自定义 `game.event` 收发。

## 消息约定

使用统一消息信封：

```json
{
  "type": "game.state",
  "requestId": "req-001",
  "roomId": "AB12",
  "senderId": "player-1",
  "seq": 12,
  "sentAt": 123456.78,
  "schema": "player-state.v1",
  "payload": {}
}
```

`payload` 归玩法模块维护。除非用户明确决定把某条规则迁移到服务器，否则不要让服务器依赖具体玩法 payload 字段。

## 部署说明

MVP 默认连接地址：

```text
ws://43.156.16.10/ws
ws://43.156.16.10:8080
```

如果 nginx 已经监听 `80`，优先使用 nginx `/ws` 反代。只有在腾讯云安全组和系统防火墙都确认放行后，才使用独立测试端口。
