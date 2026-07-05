using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Anchor.Networking
{
    public class AnchorRelayClient : MonoBehaviour
    {
        public event Action<string> MessageReceived;
        public event Action<string> StatusChanged;

        public bool IsConnected => _socket != null && _socket.State == WebSocketState.Open;

        private readonly ConcurrentQueue<string> _incoming = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<string> _outgoing = new ConcurrentQueue<string>();
        private readonly object _latestStateLock = new object();
        private ClientWebSocket _socket;
        private CancellationTokenSource _cts;
        private string _latestStateMessage;
        private bool _isSending;

        public async void Connect(string endpoint)
        {
            if (IsConnected) return;

            try
            {
                _cts = new CancellationTokenSource();
                _socket = new ClientWebSocket();
                StatusChanged?.Invoke("连接中: " + endpoint);
                await _socket.ConnectAsync(new Uri(endpoint), _cts.Token);
                StatusChanged?.Invoke("已连接");
                _ = ReceiveLoop(_cts.Token);
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke("连接失败: " + ex.Message);
                DisposeSocket();
            }
        }

        public async void Disconnect()
        {
            if (_socket == null) return;

            try
            {
                _cts?.Cancel();
                if (_socket.State == WebSocketState.Open)
                {
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "client disconnect", CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Anchor relay disconnect failed: " + ex.Message);
            }
            finally
            {
                DisposeSocket();
                StatusChanged?.Invoke("已断开");
            }
        }

        public void SendText(string json)
        {
            if (!IsConnected)
            {
                StatusChanged?.Invoke("未连接，无法发送");
                return;
            }

            if (IsGameStateMessage(json))
            {
                lock (_latestStateLock)
                {
                    _latestStateMessage = json;
                }
                return;
            }

            _outgoing.Enqueue(json);
        }

        private void Update()
        {
            if (!_isSending && IsConnected && HasQueuedMessage())
            {
                _ = SendQueuedMessages(_cts.Token);
            }

            while (_incoming.TryDequeue(out var message))
            {
                MessageReceived?.Invoke(message);
            }
        }

        private async Task SendQueuedMessages(CancellationToken token)
        {
            if (_isSending) return;
            _isSending = true;

            try
            {
                while (!token.IsCancellationRequested && IsConnected && TryDequeueNextMessage(out var json))
                {
                    var bytes = Encoding.UTF8.GetBytes(json);
                    await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when disconnecting or changing scenes.
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke("发送失败: " + ex.Message);
            }
            finally
            {
                _isSending = false;
            }
        }

        private bool HasQueuedMessage()
        {
            if (!_outgoing.IsEmpty) return true;

            lock (_latestStateLock)
            {
                return !string.IsNullOrEmpty(_latestStateMessage);
            }
        }

        private bool TryDequeueNextMessage(out string json)
        {
            if (_outgoing.TryDequeue(out json)) return true;

            lock (_latestStateLock)
            {
                json = _latestStateMessage;
                _latestStateMessage = null;
                return !string.IsNullOrEmpty(json);
            }
        }

        private static bool IsGameStateMessage(string json)
        {
            return string.Equals(AnchorJson.GetString(json, "type"), "game.state", StringComparison.Ordinal);
        }

        private async Task ReceiveLoop(CancellationToken token)
        {
            var buffer = new byte[8192];
            var builder = new StringBuilder();

            try
            {
                while (!token.IsCancellationRequested && _socket != null && _socket.State == WebSocketState.Open)
                {
                    var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close) break;

                    builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    if (!result.EndOfMessage) continue;

                    _incoming.Enqueue(builder.ToString());
                    builder.Length = 0;
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when disconnecting.
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke("接收失败: " + ex.Message);
            }
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        private void DisposeSocket()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            _socket?.Dispose();
            _socket = null;
        }
    }
}
