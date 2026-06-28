using System;

namespace Unity233.WebSocket
{
    public interface IWs233Socket : IDisposable
    {
        string Address { get; }

        Ws233State ReadyState { get; }

        Ws233BufferPool BufferPool { get; }

        event Action Opened;

        event Action<Ws233CloseCode, string> Closed;

        event Action<string> Errored;

        /// <summary>Binary frame from pooled buffer. Always call <see cref="WsBinaryMessage.Release"/>.</summary>
        event Action<WsBinaryMessage> BinaryReceived;

        void Connect();

        void Close(Ws233CloseCode code = Ws233CloseCode.Normal, string reason = "Normal Closure");

        /// <summary>Send binary payload without extra managed copies on WebGL when the payload is already a byte array.</summary>
        void Send(byte[] payload);

        /// <summary>Send binary payload from any contiguous span. WebGL copies once into reusable scratch when the span is not the byte[] fast path.</summary>
        void Send(ReadOnlySpan<byte> payload);

        void SendText(string text);
    }
}
