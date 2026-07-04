# Anchor Relay Server

Anchor 双人合作攀岩 MVP 的轻量 WebSocket relay。服务端只维护连接、房间和基础校验，`game.*` payload 原样转发，不理解攀岩玩法字段。

## 本地运行

```bash
node server.js
```

默认监听：

```text
ws://127.0.0.1:8080/ws
```

健康检查：

```text
http://127.0.0.1:8080/health
```

## 环境变量

- `HOST`：监听地址，默认 `0.0.0.0`。
- `PORT`：监听端口，默认 `8080`。
- `MAX_MESSAGE_BYTES`：单条消息大小上限，默认 `65536`。
- `MAX_ROOMS`：内存房间数量上限，默认 `100`。
- `ROOM_CODE_LENGTH`：房间码长度，默认 `4`。
- `ALLOWED_GAME_PREFIX`：允许转发的玩法消息前缀，默认 `game.`。

## 腾讯云部署步骤

1. SSH 登录 `43.156.16.10`。
2. 确认 Node.js 18+ 可用：`node -v`。
3. 创建目录：`sudo mkdir -p /opt/anchor-relay`。
4. 上传 `Server/anchor-relay` 内文件到 `/opt/anchor-relay`。
5. 在服务器执行：`cd /opt/anchor-relay && node server.js`。
6. 先用独立端口测试：`ws://43.156.16.10:8080/ws`。
7. 如果确认 nginx 可用，再使用 `deploy/nginx-anchor-relay.conf` 配置 `/ws` 反代。
8. 使用 `deploy/anchor-relay.service` 配置 systemd 保活。

MVP 内测可以先使用明文 `ws://`。承载账号、支付、正式 token 或隐私数据前再配置域名和 `wss://`。

## 不需要重新部署服务器的情况

- 修改 `game.state` payload 字段。
- 修改 `game.event` payload 字段。
- 调整客户端发送频率。
- 升级 `demo-state.v1` 到新的 schema。

这些变化只需要更新客户端 JSON 协议配置和客户端实现。
