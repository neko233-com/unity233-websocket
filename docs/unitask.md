# UniTask 支持

核心包 **不强制** 依赖 [Cysharp UniTask](https://github.com/Cysharp/UniTask)。安装 UniTask 后，`Runtime/Extensions/UniTask` 程序集自动参与编译，提供扩展方法。

---

## 安装

1. 安装 UniTask（Package Manager Git URL）：

```text
https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask
```

2. 本库已包含 `Unity233.WebSocket.UniTask.asmdef`，引用 `UniTask` + `Unity233.WebSocket`。

`manifest.json` 示例：

```json
{
  "dependencies": {
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
    "com.neko233.unity233-websocket": "https://github.com/neko233-com/unity233-websocket.git"
  }
}
```

---

## 为什么 WebGL 也适合 UniTask？

| 误解 | 事实 |
|------|------|
| UniTask = 又一层 async，WebGL 会更慢 | 扩展层只在 **等待连接/关闭** 时用 `UniTaskCompletionSource` |
| WebGL 会用 Task 收消息 | **收消息仍走 JSLIB 事件**；`RunReceiveLoopAsync` 只是 await 你的 handler |
| 与零 GC 冲突 | `SendAsync` 同步转 `Send(Span)`；消息体仍用 `WsBinaryMessage.Release()` |

WebGL 热路径 **不创建 Task**：`Connect()` / `Send()` 仍是同步 JSLIB。UniTask 用于 **业务 async 流程**（`await ConnectAsync` → `await RunReceiveLoopAsync`），比 `Task` / `Coroutine` GC 更少。

---

## API

```csharp
using Unity233.WebSocket;
using Cysharp.Threading.Tasks;

public async UniTask RunAsync(CancellationToken ct)
{
    var pool = new Ws233BufferPool();
    var socket = Ws233Socket.Create("wss://game.example/ws", pool);

    await socket.ConnectAsync(ct);

    await socket.RunReceiveLoopAsync(async frame =>
    {
        try
        {
            Parse(frame.Span);
        }
        finally
        {
            frame.Release();
        }
    }, ct);
}
```

| 方法 | 说明 |
|------|------|
| `ConnectAsync` | 等待 `Opened` 或失败/取消 |
| `CloseAsync` | 等待 `Closed` |
| `SendAsync` | 包装 `Send(ReadOnlyMemory<byte>)` |
| `SendTextAsync` | 包装 `SendText` |
| `WaitBinaryAsync` | 单条消息等待 |
| `RunReceiveLoopAsync` | Channel 驱动循环，适合 `async` 协议状态机 |

---

## 与 UnityWebSocket `*Async` 的区别

UnityWebSocket 的 `ConnectAsync` / `SendAsync` 在 WebGL 上 **并不基于 Task 完成**，多为命名习惯 + 部分平台真异步。

本库：

- **默认 API**：同步 JSLIB（WebGL 最低开销）
- **UniTask 扩展**：可选，给需要 `async/await` 的业务层；未安装 UniTask 时不编译扩展程序集

---

## 未安装 UniTask 时

仅使用核心 API：

```csharp
socket.Connect();
socket.BinaryReceived += OnBinary;
```

不会出现编译错误；`Unity233.WebSocket.UniTask.asmdef` 因缺少 `UniTask` 引用会在 Console 报错——请先安装 UniTask，或临时删除/禁用 `Runtime/Extensions/UniTask` 文件夹（不推荐，安装 UniTask 即可）。
