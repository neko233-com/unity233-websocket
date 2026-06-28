# unity233-websocket

Ultra-low-latency WebSocket client for Unity, optimized for **WebGL** with a **near-zero-GC** hot path.

A performance-focused alternative to [UnityWebSocket](https://github.com/psygames/UnityWebSocket).

**中文文档:** [README.md](README.md)

---

## Improvements over UnityWebSocket

| Area | UnityWebSocket | unity233-websocket |
|------|----------------|---------------------|
| WebGL send | `buffer.slice()` (extra JS alloc) | `Uint8Array` view (zero-copy) |
| WebGL receive (C#) | `new byte[]` per message | Pooled buffers + explicit `Release()` |
| Message API | `MessageEventArgs` class | `WsBinaryMessage` struct + `ReadOnlySpan<byte>` |
| WebGL async | `*Async` / Task | Sync JSLIB, no Task state machine |
| Protocol focus | Text + Binary | Binary-first |

**Not yet better than UnityWebSocket:** no online demo or WeChat minigame fork; ring path still requires one JS→WASM copy (unavoidable in browsers). See [docs/comparison.md](docs/comparison.md).

**Why jslib on WebGL?** [docs/webgl-jslib-explained.md](docs/webgl-jslib-explained.md)

**Documentation:** https://neko233-com.github.io/unity233-websocket/

- [Docs index](docs/index.md)
- [WebGL jslib explained](docs/webgl-jslib-explained.md)
- [Comparison with UnityWebSocket](docs/comparison.md)
- [Optimization details](docs/optimizations.md)
- [Zero-GC guide](docs/zero-gc-guide.md)
- [Migration from UnityWebSocket](docs/migration.md)

---

## Install

```text
https://github.com/neko233-com/unity233-websocket.git
```

## Example

```csharp
using Unity233.WebSocket;

var pool = new Ws233BufferPool();
var socket = Ws233Socket.Create("wss://echo.websocket.org", pool);
socket.BinaryReceived += frame =>
{
    try { /* use frame.Span */ }
    finally { frame.Release(); }
};
socket.Connect();
```

## License

MIT
