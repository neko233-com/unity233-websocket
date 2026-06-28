#if !UNITY_EDITOR && UNITY_WEBGL
using System;

namespace Unity233.WebSocket
{
    internal sealed class Ws233WebGLSocket : IWs233Socket
    {
        readonly string[] _subProtocols;
        readonly Ws233ReceiveRing _receiveRing;
        bool _disposed;

        public Ws233WebGLSocket(string address, string[] subProtocols, Ws233Options options)
        {
            options ??= new Ws233Options();
            Address = address;
            _subProtocols = subProtocols;
            BufferPool = options.BufferPool ?? new Ws233BufferPool();
            _receiveRing = new Ws233ReceiveRing(options.ReceiveRingSlotCount, options.ReceiveRingSlotSize);
            InstanceId = Ws233WebGLBridge.Allocate(address);
            Ws233WebGLBridge.BindReceiveRing(InstanceId, _receiveRing);

            if (_subProtocols == null)
            {
                return;
            }

            foreach (var protocol in _subProtocols)
            {
                if (string.IsNullOrEmpty(protocol))
                {
                    continue;
                }

                var code = Ws233WebGLBridge.WebSocket233AddSubProtocol(InstanceId, protocol);
                if (code < 0)
                {
                    Errored?.Invoke(ErrorMessage(code));
                    break;
                }
            }
        }

        public string Address { get; }

        public int InstanceId { get; }

        public Ws233BufferPool BufferPool { get; }

        internal Ws233ReceiveRing ReceiveRing => _receiveRing;

        public Ws233State ReadyState => (Ws233State)Ws233WebGLBridge.WebSocket233GetState(InstanceId);

        public event Action Opened;
        public event Action<Ws233CloseCode, string> Closed;
        public event Action<string> Errored;
        public event Action<WsBinaryMessage> BinaryReceived;

        public void Connect()
        {
            Ws233WebGLBridge.Track(this);
            var code = Ws233WebGLBridge.WebSocket233Connect(InstanceId);
            if (code < 0)
            {
                Errored?.Invoke(ErrorMessage(code));
            }
        }

        public void Close(Ws233CloseCode code = Ws233CloseCode.Normal, string reason = "Normal Closure")
        {
            var result = Ws233WebGLBridge.WebSocket233Close(InstanceId, (int)code, reason);
            if (result < 0)
            {
                Errored?.Invoke(ErrorMessage(result));
            }
        }

        public void Send(ReadOnlySpan<byte> payload)
        {
            var scratch = BufferPool.Rent(payload.Length);
            try
            {
                payload.CopyTo(scratch);
                var code = Ws233WebGLBridge.WebSocket233Send(InstanceId, scratch, payload.Length);
                if (code < 0)
                {
                    Errored?.Invoke(ErrorMessage(code));
                }
            }
            finally
            {
                BufferPool.Return(scratch);
            }
        }

        public void SendText(string text)
        {
            var code = Ws233WebGLBridge.WebSocket233SendStr(InstanceId, text);
            if (code < 0)
            {
                Errored?.Invoke(ErrorMessage(code));
            }
        }

        internal void HandleOpen() => Opened?.Invoke();

        internal void HandleBinaryFromRing(int slotIndex, int count)
        {
            BinaryReceived?.Invoke(new WsBinaryMessage(_receiveRing, slotIndex, count));
        }

        internal void HandleBinaryPooled(byte[] rented, int count)
        {
            BinaryReceived?.Invoke(new WsBinaryMessage(rented, 0, count, BufferPool));
        }

        internal void HandleError(string message) => Errored?.Invoke(message);

        internal void HandleClose(ushort code, string reason)
        {
            Closed?.Invoke((Ws233CloseCode)code, reason ?? string.Empty);
            Ws233WebGLBridge.Untrack(InstanceId);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Ws233WebGLBridge.WebSocket233Free(InstanceId);
            Ws233WebGLBridge.Untrack(InstanceId);
        }

        ~Ws233WebGLSocket()
        {
            Dispose();
        }

        static string ErrorMessage(int code)
        {
            return code switch
            {
                -1 => "WebSocket instance not found.",
                -2 => "WebSocket is already connected or connecting.",
                -3 => "WebSocket is not connected.",
                -4 => "WebSocket is already closing.",
                -5 => "WebSocket is already closed.",
                -6 => "WebSocket is not open.",
                -7 => "Invalid close code or reason.",
                -8 => "Send buffer is unsupported on this runtime.",
                -9 => "Receive ring is full; release frames or increase slot count.",
                _ => $"Unknown WebSocket error {code}.",
            };
        }
    }
}
#endif
