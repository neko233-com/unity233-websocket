# unity233-websocket

Ultra-low-latency WebSocket client for Unity, optimized for **WebGL** with a **near-zero-GC** hot path.

Designed as a performance-focused alternative to [UnityWebSocket](https://github.com/psygames/UnityWebSocket) (`psygames/UnityWebSocket`).

## Why this package

| | UnityWebSocket | unity233-websocket |
|---|---|---|
| WebGL send | `HEAPU8.buffer.slice()` (extra JS allocation) | `Uint8Array` view over WASM heap (zero-copy send) |
| Receive hot path | `new byte[msgSize]` per message | Pooled `byte[]` via `Ws233BufferPool` |
| Binary API | `byte[]` in event args | `WsBinaryMessage` + `ReadOnlySpan<byte>` |
| Text on wire | Supported | Binary-first (text helper for dev only) |

Goals:

- **0 GC on steady-state send/receive** when you reuse buffers and call `WsBinaryMessage.Release()`
- **Low latency** — direct JSLIB bridge, no Task/async on WebGL path
- **One API** for WebGL builds and Editor / Standalone (`ClientWebSocket`)

## Requirements

- Unity 2021.3+
- WebGL: enable **Run In Background** (or `Application.runInBackground = true`) so tabs do not pause the socket loop

## Install

Package Manager → **Add package from git URL**:

```text
https://github.com/neko233-com/unity233-websocket.git
```

Or in `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.neko233.unity233-websocket": "https://github.com/neko233-com/unity233-websocket.git"
  }
}
```

## Quick start

```csharp
using Unity233.WebSocket;

public sealed class GameNet
{
    readonly Ws233BufferPool _pool = new(maxPerBucket: 64);
    IWs233Socket _socket;

    public void Connect()
    {
        _socket = Ws233Socket.Create("wss://your-server.example/ws", _pool);
        _socket.Opened += () => _socket.Send(new byte[] { 1, 2, 3 });
        _socket.BinaryReceived += OnFrame;
        _socket.Connect();
    }

    void OnFrame(WsBinaryMessage frame)
    {
        ReadOnlySpan<byte> payload = frame.Span;
        // parse payload …
        frame.Release(); // return buffer to pool — required
    }
}
```

## Zero-GC checklist

1. Share one `Ws233BufferPool` for the connection lifetime.
2. Prefer `Send(ReadOnlySpan<byte>)` with stack or rented buffers.
3. Always `Release()` every `WsBinaryMessage` after parsing.
4. Avoid `SendText` / string parsing on the gameplay hot path.

## Roadmap

- [ ] Shared receive ring buffer in WASM heap (eliminate JS `_malloc` per frame)
- [ ] WebSocket compression negotiation
- [ ] WeChat / Tuanjie minigame JSLIB profile
- [ ] Benchmark scene vs UnityWebSocket / NativeWebSocket

## License

MIT — see [LICENSE](LICENSE).
