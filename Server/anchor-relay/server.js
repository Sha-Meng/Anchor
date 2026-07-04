import crypto from "node:crypto";
import http from "node:http";

const PORT = Number(process.env.PORT || 8080);
const HOST = process.env.HOST || "0.0.0.0";
const MAX_MESSAGE_BYTES = Number(process.env.MAX_MESSAGE_BYTES || 64 * 1024);
const MAX_ROOMS = Number(process.env.MAX_ROOMS || 100);
const MAX_PLAYERS_PER_ROOM = 2;
const ROOM_CODE_LENGTH = Number(process.env.ROOM_CODE_LENGTH || 4);
const ALLOWED_GAME_PREFIX = process.env.ALLOWED_GAME_PREFIX || "game.";

const WS_MAGIC = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

/** @type {Map<import('node:net').Socket, ClientState>} */
const clients = new Map();
/** @type {Map<string, RoomState>} */
const rooms = new Map();

/**
 * @typedef {Object} ClientState
 * @property {string} playerId
 * @property {string=} clientId
 * @property {string=} roomId
 * @property {Buffer} buffer
 * @property {number} seq
 * @property {number} connectedAt
 */

/**
 * @typedef {Object} RoomPlayer
 * @property {string} playerId
 * @property {import('node:net').Socket} socket
 * @property {boolean} enteredGame
 */

/**
 * @typedef {Object} RoomState
 * @property {string} roomId
 * @property {string} hostId
 * @property {"lobby"|"starting"|"inGame"|"closed"} state
 * @property {RoomPlayer[]} players
 * @property {number} createdAt
 * @property {number} lastActiveAt
 */

const server = http.createServer((req, res) => {
  if (req.url === "/health") {
    pruneEmptyRooms("health-check");
    res.writeHead(200, { "content-type": "application/json" });
    res.end(JSON.stringify({ ok: true, rooms: rooms.size, clients: clients.size }));
    return;
  }

  res.writeHead(200, { "content-type": "text/plain; charset=utf-8" });
  res.end("Anchor relay server. Use WebSocket endpoint /ws\n");
});

server.on("upgrade", (req, socket) => {
  if (req.url !== "/ws") {
    socket.write("HTTP/1.1 404 Not Found\r\n\r\n");
    socket.destroy();
    return;
  }

  const key = req.headers["sec-websocket-key"];
  if (typeof key !== "string") {
    socket.write("HTTP/1.1 400 Bad Request\r\n\r\n");
    socket.destroy();
    return;
  }

  const accept = crypto.createHash("sha1").update(key + WS_MAGIC).digest("base64");
  socket.write([
    "HTTP/1.1 101 Switching Protocols",
    "Upgrade: websocket",
    "Connection: Upgrade",
    `Sec-WebSocket-Accept: ${accept}`,
    "",
    ""
  ].join("\r\n"));

  const state = {
    playerId: makePlayerId(),
    buffer: Buffer.alloc(0),
    seq: 0,
    connectedAt: Date.now()
  };
  clients.set(socket, state);
  log("client.connected", { playerId: state.playerId, remote: socket.remoteAddress });
  send(socket, {
    type: "system.welcome",
    payload: {
      playerId: state.playerId,
      serverTime: nowSeconds()
    }
  });

  socket.on("data", (chunk) => handleSocketData(socket, chunk));
  socket.on("close", () => cleanupClient(socket, "close"));
  socket.on("error", (error) => {
    log("client.error", { playerId: state.playerId, error: error.message });
    cleanupClient(socket, "error");
  });
});

server.listen(PORT, HOST, () => {
  log("server.started", { host: HOST, port: PORT, path: "/ws" });
});

function handleSocketData(socket, chunk) {
  const state = clients.get(socket);
  if (!state) return;

  state.buffer = Buffer.concat([state.buffer, chunk]);
  while (true) {
    const frame = readFrame(state.buffer);
    if (!frame) return;
    state.buffer = frame.remaining;

    if (frame.opcode === 0x8) {
      cleanupClient(socket, "close-frame");
      socket.end();
      return;
    }

    if (frame.opcode === 0x9) {
      writeFrame(socket, frame.payload, 0xA);
      continue;
    }

    if (frame.opcode !== 0x1) continue;
    if (frame.payload.length > MAX_MESSAGE_BYTES) {
      sendError(socket, undefined, "MESSAGE_TOO_LARGE", "消息过大");
      continue;
    }

    const text = frame.payload.toString("utf8");
    handleMessage(socket, text);
  }
}

function handleMessage(socket, text) {
  const client = clients.get(socket);
  if (!client) return;

  /** @type {any} */
  let message;
  try {
    message = JSON.parse(text);
  } catch {
    sendError(socket, undefined, "INVALID_MESSAGE", "消息不是合法 JSON");
    log("message.invalidJson", { playerId: client.playerId });
    return;
  }

  if (!message || typeof message.type !== "string") {
    sendError(socket, message?.requestId, "INVALID_MESSAGE", "缺少消息 type");
    return;
  }

  switch (message.type) {
    case "system.hello":
      client.clientId = readString(message.payload?.clientId);
      send(socket, {
        type: "system.welcome",
        requestId: message.requestId,
        payload: { playerId: client.playerId, serverTime: nowSeconds() }
      });
      break;
    case "room.list":
      handleRoomList(socket, message);
      break;
    case "room.create":
      handleRoomCreate(socket, message);
      break;
    case "room.join":
      handleRoomJoin(socket, message);
      break;
    case "room.start":
      handleRoomStart(socket, message);
      break;
    case "room.enteredGame":
      handleEnteredGame(socket, message);
      break;
    case "room.leave":
      leaveRoom(socket, "leave");
      break;
    default:
      if (message.type.startsWith(ALLOWED_GAME_PREFIX)) {
        relayGameMessage(socket, message);
      } else {
        sendError(socket, message.requestId, "INVALID_MESSAGE", `不支持的消息类型: ${message.type}`);
      }
      break;
  }
}

function handleRoomList(socket, message) {
  send(socket, {
    type: "room.list.result",
    requestId: message.requestId,
    payload: { rooms: getRoomListPayload() }
  });
}

function handleRoomCreate(socket, message) {
  const client = clients.get(socket);
  if (!client) return;
  if (rooms.size >= MAX_ROOMS) {
    sendError(socket, message.requestId, "ROOM_LIMIT", "房间数量已达上限");
    return;
  }

  leaveRoom(socket, "create-new-room", false);
  const roomId = makeRoomId();
  const room = {
    roomId,
    hostId: client.playerId,
    state: "lobby",
    players: [{ playerId: client.playerId, socket, enteredGame: false }],
    createdAt: Date.now(),
    lastActiveAt: Date.now()
  };
  rooms.set(roomId, room);
  client.roomId = roomId;

  send(socket, {
    type: "room.created",
    requestId: message.requestId,
    roomId,
    payload: { roomId, hostId: room.hostId, state: room.state }
  });
  broadcastRoomUpdated(room);
  broadcastRoomListUpdated();
  log("room.created", { roomId, hostId: room.hostId });
}

function handleRoomJoin(socket, message) {
  const client = clients.get(socket);
  if (!client) return;

  const roomId = readString(message.roomId || message.payload?.roomId);
  const room = roomId ? rooms.get(roomId) : undefined;
  if (!room || room.state === "closed") {
    sendError(socket, message.requestId, "ROOM_NOT_FOUND", "房间不存在或已关闭");
    return;
  }
  if (room.state !== "lobby") {
    sendError(socket, message.requestId, "INVALID_ROOM_STATE", "当前房间不可加入");
    return;
  }
  if (room.players.length >= MAX_PLAYERS_PER_ROOM) {
    sendError(socket, message.requestId, "ROOM_FULL", "房间已满");
    return;
  }
  if (room.players.some((player) => player.socket === socket)) {
    sendError(socket, message.requestId, "ALREADY_IN_ROOM", "已经在该房间中");
    return;
  }

  leaveRoom(socket, "join-new-room", false);
  room.players.push({ playerId: client.playerId, socket, enteredGame: false });
  room.lastActiveAt = Date.now();
  client.roomId = room.roomId;

  send(socket, {
    type: "room.joined",
    requestId: message.requestId,
    roomId: room.roomId,
    payload: { roomId: room.roomId, hostId: room.hostId, state: room.state }
  });
  broadcastRoomUpdated(room);
  broadcastRoomListUpdated();
  log("room.joined", { roomId: room.roomId, playerId: client.playerId });
}

function handleRoomStart(socket, message) {
  const client = clients.get(socket);
  const room = getClientRoom(socket);
  if (!client || !room) {
    sendError(socket, message.requestId, "ROOM_NOT_FOUND", "不在房间中");
    return;
  }
  if (room.hostId !== client.playerId) {
    sendError(socket, message.requestId, "NOT_HOST", "非房主不能开始");
    return;
  }
  if (room.state !== "lobby") {
    sendError(socket, message.requestId, "INVALID_ROOM_STATE", "当前房间状态不能开始");
    return;
  }
  if (room.players.length !== MAX_PLAYERS_PER_ROOM) {
    sendError(socket, message.requestId, "ROOM_NOT_READY", "房间未满员");
    return;
  }

  room.state = "starting";
  room.lastActiveAt = Date.now();
  const countdownMs = Number(message.payload?.countdownMs || 1000);
  const startAt = nowSeconds() + countdownMs / 1000;
  broadcast(room, {
    type: "room.starting",
    roomId: room.roomId,
    payload: { countdownMs, startAt }
  });
  broadcastRoomListUpdated();
  log("room.starting", { roomId: room.roomId, hostId: room.hostId });
}

function handleEnteredGame(socket, message) {
  const client = clients.get(socket);
  const room = getClientRoom(socket);
  if (!client || !room) {
    sendError(socket, message.requestId, "ROOM_NOT_FOUND", "不在房间中");
    return;
  }

  const player = room.players.find((item) => item.socket === socket);
  if (player) player.enteredGame = true;
  room.lastActiveAt = Date.now();

  if (room.players.length === MAX_PLAYERS_PER_ROOM && room.players.every((item) => item.enteredGame)) {
    room.state = "inGame";
    broadcast(room, {
      type: "room.inGame",
      roomId: room.roomId,
      payload: {}
    });
    broadcastRoomListUpdated();
    log("room.inGame", { roomId: room.roomId });
  }
}

function relayGameMessage(socket, message) {
  const room = getClientRoom(socket);
  const client = clients.get(socket);
  if (!room || !client) {
    sendError(socket, message.requestId, "ROOM_NOT_FOUND", "不在房间中");
    return;
  }
  if (room.state !== "inGame") {
    sendError(socket, message.requestId, "INVALID_ROOM_STATE", "房间尚未进入游戏");
    return;
  }

  room.lastActiveAt = Date.now();
  const outgoing = {
    ...message,
    roomId: room.roomId,
    senderId: client.playerId
  };
  for (const player of room.players) {
    if (player.socket !== socket) send(player.socket, outgoing);
  }
}

function leaveRoom(socket, reason, notify = true) {
  const client = clients.get(socket);
  if (!client?.roomId) return;

  const room = rooms.get(client.roomId);
  client.roomId = undefined;
  if (!room) return;

  room.players = room.players.filter((player) => player.socket !== socket);
  room.lastActiveAt = Date.now();

  if (notify) {
    broadcast(room, {
      type: "room.peerLeft",
      roomId: room.roomId,
      payload: { playerId: client.playerId }
    });
  }

  if (room.players.length === 0) {
    room.state = "closed";
    rooms.delete(room.roomId);
    broadcastRoomListUpdated();
    log("room.closed", { roomId: room.roomId, reason });
    return;
  }

  if (room.hostId === client.playerId) {
    room.hostId = room.players[0].playerId;
  }
  room.state = "lobby";
  for (const player of room.players) player.enteredGame = false;
  broadcastRoomUpdated(room);
  broadcastRoomListUpdated();
  log("room.left", { roomId: room.roomId, playerId: client.playerId, reason });
}

function cleanupClient(socket, reason) {
  const client = clients.get(socket);
  if (!client) return;
  leaveRoom(socket, reason);
  clients.delete(socket);
  log("client.disconnected", { playerId: client.playerId, reason });
}

function broadcastRoomUpdated(room) {
  broadcast(room, {
    type: "room.updated",
    roomId: room.roomId,
    payload: {
      roomId: room.roomId,
      hostId: room.hostId,
      state: room.state,
      playerCount: room.players.length,
      maxPlayers: MAX_PLAYERS_PER_ROOM,
      players: room.players.map((player) => ({
        playerId: player.playerId,
        role: player.playerId === room.hostId ? "host" : "guest",
        isHost: player.playerId === room.hostId
      }))
    }
  });
}

function getRoomListPayload() {
  pruneEmptyRooms("room-list");

  return Array.from(rooms.values())
    .filter((room) => room.state !== "closed" && room.players.length > 0)
    .map((room) => ({
      roomId: room.roomId,
      hostId: room.hostId,
      playerCount: room.players.length,
      maxPlayers: MAX_PLAYERS_PER_ROOM,
      state: room.state,
      canJoin: room.state === "lobby" && room.players.length < MAX_PLAYERS_PER_ROOM
    }));
}

function pruneEmptyRooms(reason) {
  for (const [roomId, room] of rooms) {
    if (room.state !== "closed" && room.players.length > 0) continue;

    room.state = "closed";
    rooms.delete(roomId);
    log("room.closed", { roomId, reason });
  }
}

function broadcastRoomListUpdated() {
  const message = {
    type: "room.list.updated",
    payload: { rooms: getRoomListPayload() }
  };

  for (const socket of clients.keys()) {
    send(socket, message);
  }
}

function broadcast(room, message) {
  for (const player of room.players) {
    send(player.socket, message);
  }
}

function sendError(socket, requestId, code, message) {
  send(socket, {
    type: "system.error",
    requestId,
    payload: { code, message }
  });
}

function send(socket, message) {
  if (socket.destroyed) return;
  writeFrame(socket, Buffer.from(JSON.stringify(message), "utf8"), 0x1);
}

function readFrame(buffer) {
  if (buffer.length < 2) return null;

  const first = buffer[0];
  const second = buffer[1];
  const opcode = first & 0x0f;
  const masked = (second & 0x80) !== 0;
  let length = second & 0x7f;
  let offset = 2;

  if (length === 126) {
    if (buffer.length < offset + 2) return null;
    length = buffer.readUInt16BE(offset);
    offset += 2;
  } else if (length === 127) {
    if (buffer.length < offset + 8) return null;
    const high = buffer.readUInt32BE(offset);
    const low = buffer.readUInt32BE(offset + 4);
    if (high !== 0) throw new Error("Frame too large");
    length = low;
    offset += 8;
  }

  let mask;
  if (masked) {
    if (buffer.length < offset + 4) return null;
    mask = buffer.subarray(offset, offset + 4);
    offset += 4;
  }

  if (buffer.length < offset + length) return null;
  const payload = Buffer.from(buffer.subarray(offset, offset + length));
  if (mask) {
    for (let index = 0; index < payload.length; index += 1) {
      payload[index] ^= mask[index % 4];
    }
  }

  return {
    opcode,
    payload,
    remaining: buffer.subarray(offset + length)
  };
}

function writeFrame(socket, payload, opcode) {
  const length = payload.length;
  let header;
  if (length < 126) {
    header = Buffer.from([0x80 | opcode, length]);
  } else if (length < 65536) {
    header = Buffer.alloc(4);
    header[0] = 0x80 | opcode;
    header[1] = 126;
    header.writeUInt16BE(length, 2);
  } else {
    header = Buffer.alloc(10);
    header[0] = 0x80 | opcode;
    header[1] = 127;
    header.writeUInt32BE(0, 2);
    header.writeUInt32BE(length, 6);
  }

  socket.write(Buffer.concat([header, payload]));
}

function getClientRoom(socket) {
  const client = clients.get(socket);
  return client?.roomId ? rooms.get(client.roomId) : undefined;
}

function makePlayerId() {
  return `p_${crypto.randomBytes(3).toString("hex")}`;
}

function makeRoomId() {
  const alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
  for (let attempt = 0; attempt < 100; attempt += 1) {
    let value = "";
    for (let index = 0; index < ROOM_CODE_LENGTH; index += 1) {
      value += alphabet[Math.floor(Math.random() * alphabet.length)];
    }
    if (!rooms.has(value)) return value;
  }
  return crypto.randomBytes(3).toString("hex").toUpperCase();
}

function readString(value) {
  return typeof value === "string" && value.trim().length > 0 ? value.trim() : undefined;
}

function nowSeconds() {
  return Date.now() / 1000;
}

function log(event, data = {}) {
  console.log(JSON.stringify({ time: new Date().toISOString(), event, ...data }));
}
