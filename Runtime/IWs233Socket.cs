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

        /// <summary>Send binary payload without extra copies on WebGL (Uint8Array view over WASM heap).</summary>
        void Send(ReadOnlySpan<byte> payload);

        void SendText(string text);
    }
}
