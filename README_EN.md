# unity233-websocket

Ultra-low-latency WebSocket client for Unity, optimized for **WebGL** with a **near-zero-GC** hot path.

Performance-focused alternative to [UnityWebSocket](https://github.com/psygames/UnityWebSocket).

## Install

```text
https://github.com/neko233-com/unity233-websocket.git
```

## Example

```csharp
using Unity233.WebSocket;

var pool = new Ws233BufferPool();
var socket = Ws233Socket.Create("wss://echo.websocket.org", pool);
socket.BinaryReceived += frame => { /* use frame.Span */ frame.Release(); };
socket.Connect();
```

See [README.md](README.md) (Chinese) for full documentation.

## License

MIT
