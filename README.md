# unity233-websocket

Ultra-low-latency WebSocket client for Unity, optimized for **WebGL** with a **near-zero-GC** hot path.

以 [psygames/UnityWebSocket](https://github.com/psygames/UnityWebSocket) 为参照实现，在 API 简洁的前提下，针对 **WebGL 高频 Binary 收发** 做 GC 与拷贝路径优化。

**English:** [README_EN.md](README_EN.md)

---

## 相对 UnityWebSocket 做了什么

| 优化项 | UnityWebSocket | 本库 |
|--------|----------------|------|
| WebGL 发送 | `HEAPU8.buffer.slice()` → JS 堆分配 | `Uint8Array` view → **零拷贝发送** |
| WebGL 接收（JS） | 每条 `_malloc` / `_free` | **`Ws233ReceiveRing` 直写 WASM**（稳态无 malloc） |
| WebGL 接收（C#） | 每条 `new byte[msgSize]` + `Marshal.Copy` | 环形槽 **零拷贝**；降级才走 Pool |
| 消息 API | `MessageEventArgs`（class + `byte[]`） | `WsBinaryMessage`（struct + `ReadOnlySpan<byte>`） |
| 异步 | `*Async` 命名 | 核心同步 JSLIB + **可选 UniTask 扩展** |
| 协议取向 | Text / Binary 并重 | **Binary-first**（竞技 / 帧同步场景） |

**在线文档：** https://neko233-com.github.io/unity233-websocket/

**WebGL 为何必须 jslib？** 不是「外挂 DLL」，而是浏览器沙箱硬约束 → [docs/webgl-jslib-explained.md](docs/webgl-jslib-explained.md)

**详细文档：**

- [📚 文档首页](docs/index.md)
- [为什么 WebGL 必须挂 JS？](docs/webgl-jslib-explained.md)
- [与 UnityWebSocket 对比](docs/comparison.md)
- [优化原理详解](docs/optimizations.md)
- [UniTask 支持](docs/unitask.md)
- [零 GC 使用指南](docs/zero-gc-guide.md)
- [从 UnityWebSocket 迁移](docs/migration.md)

---

## 架构（发送 / 接收）

```mermaid
flowchart TB
    subgraph Send["发送（已优化）"]
        CS[业务 Span/byte[]] --> Pool[Ws233BufferPool scratch]
        Pool --> JSLIB[WebSocket233.jslib]
        JSLIB --> View["Uint8Array view（无 slice）"]
        View --> WS[Browser WebSocket]
    end

    subgraph Recv["接收（v0.2 环形缓冲）"]
        WS2[Browser onmessage] --> Ring{Ring 有空槽?}
        Ring -->|是| Set[HEAPU8.set 直写槽位]
        Set --> Handler[BinaryReceived]
        Ring -->|否/超大帧| Malloc[降级 _malloc + Pool]
        Malloc --> Handler
        Handler --> Release[frame.Release]
    end
```

---

## 环境要求

- Unity **2021.3+**（含 Unity 6 `makeDynCall`）
- WebGL：建议 `Application.runInBackground = true`

---

## 安装

Package Manager → **Add package from git URL**：

```text
https://github.com/neko233-com/unity233-websocket.git
```

`Packages/manifest.json`：

```json
{
  "dependencies": {
    "com.neko233.unity233-websocket": "https://github.com/neko233-com/unity233-websocket.git"
  }
}
```

---

## 快速开始

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
        try
        {
            ReadOnlySpan<byte> payload = frame.Span;
            // 解析 payload …
        }
        finally
        {
            frame.Release(); // 必须：归还缓冲到 pool
        }
    }
}
```

Sample：`Samples~/Demo/Ws233Demo.cs`

### UniTask（可选）

安装 [UniTask](https://github.com/Cysharp/UniTask) 后：

```csharp
await socket.ConnectAsync(ct);
await socket.RunReceiveLoopAsync(async frame => {
    try { Parse(frame.Span); }
    finally { frame.Release(); }
}, ct);
```

见 [docs/unitask.md](docs/unitask.md)

---

## 零 GC 要点

1. 连接生命周期内**共享一个** `Ws233BufferPool`
2. 热路径使用 `Send(ReadOnlySpan<byte>)`，避免 `SendText`
3. **每条**收到的 `WsBinaryMessage` 必须 `Release()`
4. 解析时用 `frame.Span`，避免 `ToArray()`

完整说明：[docs/zero-gc-guide.md](docs/zero-gc-guide.md)

---

## Roadmap

- [ ] SharedArrayBuffer / 批量 drain
- [ ] WebSocket 压缩扩展协商
- [ ] 微信 / 团结小游戏 JSLIB
- [ ] 与 UnityWebSocket / NativeWebSocket 的 Benchmark 场景

---

## License

MIT — see [LICENSE](LICENSE).
