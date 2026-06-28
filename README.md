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
| 异步 | `*Async` 命名 | 核心同步 JSLIB；运行时 **0 第三方依赖** |
| 协议取向 | Text / Binary 并重 | **Binary-first**（竞技 / 帧同步场景） |

## 预估性能收益

> 下面是基于实现路径的工程预估，不是同机同场景实测。  
> 实际收益取决于消息频率、包大小、是否长期忘记 `Release()`、以及是否启用 WebGL 环形接收路径。

| 场景 | UnityWebSocket 常见成本 | 本库预估变化 | 典型收益 |
|------|--------------------------|--------------|----------|
| WebGL 发送（Binary） | 每包 `buffer.slice()`，产生新的 `ArrayBuffer` | 改为 `Uint8Array` view，消除 JS 额外拷贝 | JS 侧发送分配可视为 **100% 消除** |
| WebGL 接收（非 ring） | 每包 `new byte[]` + `Marshal.Copy` | 改为 `BufferPool` 复用 | 稳态下 GC Alloc 通常可降 **70%~95%** |
| WebGL 接收（ring 稳态） | 每包 `_malloc` / `_free` + `new byte[]` | 预分配环形槽，稳态无 per-msg malloc | 热路径 GC 可接近 **0** |
| 长时间运行 / 微信小游戏 | JS heap 随消息数上涨，OOM 风险高 | 清理 WebSocket 引用并释放实例 | 更适合长稳态运行，OOM 风险显著下降 |
| Text / JSON 热路径 | 字符串与 UTF8 额外分配 | 本库不主推 Text 接收 | 若仍以 Text 为主，提升有限 |

> 粗略结论：如果你的场景是 **WebGL / 微信小游戏 + 高频 Binary**，常见体感不是“快一点”，而是 **内存增长被压住、卡顿峰值明显减少**。  
> 在这类场景里，端到端吞吐提升通常不会夸张到数倍，但 **20%~40% 的主线程压力下降** 和 **接近 0 的 GC Alloc** 往往是能看到的目标区间。

## 当前 WebGL 自动化实测

独立 benchmark 仓库：`UnityWebsocket-benchmark`。  
测试方式：Unity `2022.3.51f1` WebGL 构建，Playwright Chromium 自动打开页面，WebSocket 与静态服务器均绑定 `0.0.0.0`，浏览器使用 LAN IP，避免 `127.0.0.1` / loopback 优化；二进制 payload 为随机内容并带 checksum 校验。参数：`60s` duration，`5s` warmup，`256B`，`1000Hz`。

| Client | Sent/s | Received/s | Bad payloads | Managed memory delta | GC delta | Max frame delta |
|--------|--------|------------|--------------|----------------------|----------|-----------------|
| `unity233-websocket` | `1000.16` | `1145.39` | `0` | `0 B` | `1 / 1 / 1` | `37.90 ms` |
| `psygames/UnityWebSocket` | `999.52` | `1144.39` | `0` | `24,576 B` | `208 / 208 / 208` | `34.70 ms` |

这个 WebGL 压测说明：在同一条浏览器 WebSocket 管道接近极限时，吞吐是同级别；本库真正拉开差距的是 GC 次数。官方 UnityWebSocket 的托管 GC delta 会随时间 / 消息量持续累积，`20s` 压测约 `69 / 69 / 69`，`60s` 压测约 `208 / 208 / 208`；本库稳定在 `1 / 1 / 1`。

**在线文档：** https://neko233-com.github.io/unity233-websocket/

**WebGL 为何必须 jslib？** → [docs/webgl-jslib-explained.md](docs/webgl-jslib-explained.md)

**UnityWebSocket JS 内存泄漏 / 微信 OOM？** → [docs/memory-leaks.md](docs/memory-leaks.md)

**详细文档：**

- [📚 文档首页](docs/index.md)
- [为什么 WebGL 必须挂 JS？](docs/webgl-jslib-explained.md)
- [**UnityWebSocket 内存泄漏与 OOM 修复**](docs/memory-leaks.md)
- [与 UnityWebSocket 对比](docs/comparison.md)
- [优化原理详解](docs/optimizations.md)
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

### 微信小游戏

```csharp
var socket = Ws233Socket.Create(url, Ws233Options.WeChatMinigameDefaults);
```

务必 `frame.Release()` + 连接 `Dispose()`，见 [docs/memory-leaks.md](docs/memory-leaks.md)。

---

## 零 GC 要点

1. 连接生命周期内**共享一个** `Ws233BufferPool`
2. 热路径使用 `Send(ReadOnlySpan<byte>)`，避免 `SendText`
3. **每条**收到的 `WsBinaryMessage` 必须 `Release()`
4. 解析时用 `frame.Span`，避免 `ToArray()`

完整说明：[docs/zero-gc-guide.md](docs/zero-gc-guide.md)

---

## 自动化验证

本库包含 Unity EditMode 单元测试，覆盖 `Ws233BufferPool`、`Ws233ReceiveRing`、`WsBinaryMessage`、`Ws233Options` 与基础 socket factory 行为。

在完整 Unity 工程中安装本包后，可通过 Test Runner 跑：

```text
Window > General > Test Runner > EditMode > Unity233.WebSocket.Tests
```

命令行示例：

```powershell
Unity.exe -batchmode -quit -projectPath <YourUnityProject> -runTests -testPlatform EditMode -testResults TestResults.xml
```

官方 UnityWebSocket 的 WebGL 泄漏风险审计脚本放在：

```text
Verification/UnityWebSocketLeakAudit
```

---

## Roadmap

- [ ] SharedArrayBuffer / 批量 drain
- [ ] WebSocket 压缩扩展协商
- [ ] 微信 / 团结小游戏 JSLIB
- [ ] 与 UnityWebSocket / NativeWebSocket 的 Benchmark 场景

---

## License

MIT — see [LICENSE](LICENSE).
