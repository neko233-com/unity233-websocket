using System.Text;
using UnityEngine;

namespace Unity233.WebSocket.Samples
{
    public sealed class Ws233Demo : MonoBehaviour
    {
        [SerializeField] string _address = "wss://echo.websocket.org";

        IWs233Socket _socket;
        Ws233BufferPool _pool;

        void Start()
        {
            Application.runInBackground = true;

            _pool = new Ws233BufferPool(maxPerBucket: 64);
            _socket = Ws233Socket.Create(_address, _pool);

            _socket.Opened += OnOpen;
            _socket.Closed += OnClose;
            _socket.Errored += OnError;
            _socket.BinaryReceived += OnBinary;

            _socket.Connect();
        }

        void OnOpen()
        {
            Debug.Log("[Ws233] connected");
            var payload = Encoding.UTF8.GetBytes("hello from unity233-websocket");
            _socket.Send(payload);
        }

        void OnBinary(WsBinaryMessage message)
        {
            Debug.Log($"[Ws233] binary {message.Count} bytes: {Encoding.UTF8.GetString(message.Span)}");
            message.Release();
            _socket.Close();
        }

        void OnClose(Ws233CloseCode code, string reason)
        {
            Debug.Log($"[Ws233] closed code={(ushort)code} reason={reason}");
        }

        void OnError(string error)
        {
            Debug.LogError($"[Ws233] error: {error}");
        }

        void OnDestroy()
        {
            _socket?.Dispose();
        }
    }
}
