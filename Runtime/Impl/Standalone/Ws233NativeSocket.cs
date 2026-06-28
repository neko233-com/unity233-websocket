#if UNITY_EDITOR || !UNITY_WEBGL
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Unity233.WebSocket
{
    internal sealed class Ws233NativeSocket : IWs233Socket
    {
        readonly ClientWebSocket _socket = new();
        readonly CancellationTokenSource _lifetime = new();
        readonly string[] _subProtocols;
        readonly byte[] _receiveScratch = new byte[8192];

        bool _disposed;
        Task _pump;

        public Ws233NativeSocket(string address, string[] subProtocols, Ws233Options options)
        {
            options ??= new Ws233Options();
            Address = address;
            _subProtocols = subProtocols;
            BufferPool = options.BufferPool ?? new Ws233BufferPool();
        }

        public string Address { get; }

        public Ws233BufferPool BufferPool { get; }

        public Ws233State ReadyState
        {
            get
            {
                return _socket.State switch
                {
                    WebSocketState.Connecting => Ws233State.Connecting,
                    WebSocketState.Open => Ws233State.Open,
                    WebSocketState.CloseSent or WebSocketState.CloseReceived => Ws233State.Closing,
                    _ => Ws233State.Closed,
                };
            }
        }

        public event Action Opened;
        public event Action<Ws233CloseCode, string> Closed;
        public event Action<string> Errored;
        public event Action<WsBinaryMessage> BinaryReceived;

        public void Connect()
        {
            _pump = PumpAsync();
        }

        public void Close(Ws233CloseCode code = Ws233CloseCode.Normal, string reason = "Normal Closure")
        {
            _ = CloseAsync(code, reason);
        }

        public void Send(ReadOnlySpan<byte> payload)
        {
            if (_socket.State != WebSocketState.Open)
            {
                Errored?.Invoke("WebSocket is not open.");
                return;
            }

            var scratch = BufferPool.Rent(payload.Length);
            payload.CopyTo(scratch);
            _ = SendBinaryAsync(scratch, payload.Length);
        }

        async Task SendBinaryAsync(byte[] scratch, int length)
        {
            try
            {
                await _socket.SendAsync(
                    new ArraySegment<byte>(scratch, 0, length),
                    WebSocketMessageType.Binary,
                    true,
                    _lifetime.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Errored?.Invoke(ex.Message);
            }
            finally
            {
                BufferPool.Return(scratch);
            }
        }

        public void SendText(string text)
        {
            if (_socket.State != WebSocketState.Open)
            {
                Errored?.Invoke("WebSocket is not open.");
                return;
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(text);
            _ = _socket.SendAsync(bytes, WebSocketMessageType.Text, true, _lifetime.Token);
        }

        async Task PumpAsync()
        {
            try
            {
                if (_subProtocols != null)
                {
                    foreach (var protocol in _subProtocols)
                    {
                        if (!string.IsNullOrEmpty(protocol))
                        {
                            _socket.Options.AddSubProtocol(protocol);
                        }
                    }
                }

                await _socket.ConnectAsync(new Uri(Address), _lifetime.Token).ConfigureAwait(false);
                Opened?.Invoke();

                while (_socket.State == WebSocketState.Open && !_lifetime.IsCancellationRequested)
                {
                    var result = await _socket.ReceiveAsync(_receiveScratch, _lifetime.Token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    if (result.MessageType != WebSocketMessageType.Binary || result.Count == 0)
                    {
                        continue;
                    }

                    var rented = BufferPool.Rent(result.Count);
                    Buffer.BlockCopy(_receiveScratch, 0, rented, 0, result.Count);
                    BinaryReceived?.Invoke(new WsBinaryMessage(rented, 0, result.Count, BufferPool));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Errored?.Invoke(ex.Message);
            }
            finally
            {
                var code = _socket.CloseStatus.HasValue
                    ? (Ws233CloseCode)_socket.CloseStatus.Value
                    : Ws233CloseCode.NoStatus;
                var reason = _socket.CloseStatusDescription ?? string.Empty;
                Closed?.Invoke(code, reason);
            }
        }

        async Task CloseAsync(Ws233CloseCode code, string reason)
        {
            if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try
                {
                    await _socket.CloseAsync(
                        (WebSocketCloseStatus)code,
                        reason,
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Errored?.Invoke(ex.Message);
                }
            }

            _lifetime.Cancel();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lifetime.Cancel();
            _lifetime.Dispose();
            _socket.Dispose();
        }
    }
}
#endif
