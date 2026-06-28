using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Unity233.WebSocket
{
    /// <summary>
    /// UniTask adapters for <see cref="IWs233Socket"/>. Requires Cysharp UniTask (com.cysharp.unitask).
    /// WebGL uses the same zero-GC JSLIB path; UniTask only replaces Task/event waiting without extra allocations when configured with AsyncUniTaskVoid pooling.
    /// </summary>
    public static class Ws233SocketUniTaskExtensions
    {
        public static UniTask ConnectAsync(this IWs233Socket socket, CancellationToken cancellationToken = default)
        {
            if (socket == null)
            {
                throw new ArgumentNullException(nameof(socket));
            }

            if (socket.ReadyState == Ws233State.Open)
            {
                return UniTask.CompletedTask;
            }

            var tcs = new UniTaskCompletionSource();

            void Cleanup()
            {
                socket.Opened -= OnOpen;
                socket.Errored -= OnError;
                socket.Closed -= OnClosed;
            }

            void OnOpen()
            {
                Cleanup();
                tcs.TrySetResult();
            }

            void OnError(string message)
            {
                Cleanup();
                tcs.TrySetException(new InvalidOperationException(message));
            }

            void OnClosed(Ws233CloseCode code, string reason)
            {
                if (socket.ReadyState == Ws233State.Open)
                {
                    return;
                }

                Cleanup();
                tcs.TrySetException(new InvalidOperationException($"Closed before open: {(ushort)code} {reason}"));
            }

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() =>
                {
                    Cleanup();
                    socket.Close();
                    tcs.TrySetCanceled(cancellationToken);
                });
            }

            socket.Opened += OnOpen;
            socket.Errored += OnError;
            socket.Closed += OnClosed;
            socket.Connect();

            return tcs.Task;
        }

        public static UniTask CloseAsync(
            this IWs233Socket socket,
            Ws233CloseCode code = Ws233CloseCode.Normal,
            string reason = "Normal Closure",
            CancellationToken cancellationToken = default)
        {
            if (socket == null)
            {
                throw new ArgumentNullException(nameof(socket));
            }

            if (socket.ReadyState is Ws233State.Closed or Ws233State.Closing)
            {
                return UniTask.CompletedTask;
            }

            var tcs = new UniTaskCompletionSource();

            void Cleanup()
            {
                socket.Closed -= OnClosed;
                socket.Errored -= OnError;
            }

            void OnClosed(Ws233CloseCode _, string __)
            {
                Cleanup();
                tcs.TrySetResult();
            }

            void OnError(string message)
            {
                Cleanup();
                tcs.TrySetException(new InvalidOperationException(message));
            }

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() =>
                {
                    Cleanup();
                    tcs.TrySetCanceled(cancellationToken);
                });
            }

            socket.Closed += OnClosed;
            socket.Errored += OnError;
            socket.Close(code, reason);

            return tcs.Task;
        }

        public static UniTask SendAsync(this IWs233Socket socket, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
        {
            if (socket == null)
            {
                throw new ArgumentNullException(nameof(socket));
            }

            cancellationToken.ThrowIfCancellationRequested();
            socket.Send(payload.Span);
            return UniTask.CompletedTask;
        }

        public static UniTask SendTextAsync(this IWs233Socket socket, string text, CancellationToken cancellationToken = default)
        {
            if (socket == null)
            {
                throw new ArgumentNullException(nameof(socket));
            }

            cancellationToken.ThrowIfCancellationRequested();
            socket.SendText(text);
            return UniTask.CompletedTask;
        }

        public static UniTask<WsBinaryMessage> WaitBinaryAsync(this IWs233Socket socket, CancellationToken cancellationToken = default)
        {
            if (socket == null)
            {
                throw new ArgumentNullException(nameof(socket));
            }

            var tcs = new UniTaskCompletionSource<WsBinaryMessage>();

            void Cleanup()
            {
                socket.BinaryReceived -= OnBinary;
            }

            void OnBinary(WsBinaryMessage message)
            {
                Cleanup();
                tcs.TrySetResult(message);
            }

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() =>
                {
                    Cleanup();
                    tcs.TrySetCanceled(cancellationToken);
                });
            }

            socket.BinaryReceived += OnBinary;
            return tcs.Task;
        }

        public static async UniTask RunReceiveLoopAsync(
            this IWs233Socket socket,
            Func<WsBinaryMessage, UniTask> handler,
            CancellationToken cancellationToken = default)
        {
            if (socket == null)
            {
                throw new ArgumentNullException(nameof(socket));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var channel = Channel.CreateSingleConsumerUnbounded<WsBinaryMessage>();

            void OnBinary(WsBinaryMessage message)
            {
                channel.Writer.TryWrite(message);
            }

            socket.BinaryReceived += OnBinary;

            try
            {
                while (await channel.Reader.WaitToReadAsync(cancellationToken))
                {
                    while (channel.Reader.TryRead(out var message))
                    {
                        await handler(message);
                    }
                }
            }
            finally
            {
                socket.BinaryReceived -= OnBinary;
                channel.Writer.TryComplete();
            }
        }
    }
}
