# 从 UnityWebSocket 迁移

参考项目：[psygames/UnityWebSocket](https://github.com/psygames/UnityWebSocket)

---

## 安装切换

**移除**（`Packages/manifest.json`）：

```json
"com.psygames.unitywebsocket": "https://github.com/psygames/UnityWebSocket.git#upm"
```

**添加**：

```json
"com.neko233.unity233-websocket": "https://github.com/neko233-com/unity233-websocket.git"
```

> 两个插件 JSLIB 符号不同，可短暂共存做 A/B 测试，但生产环境只保留一个即可。

---

## API 对照

| UnityWebSocket | unity233-websocket |
|----------------|---------------------|
| `using UnityWebSocket;` | `using Unity233.WebSocket;` |
| `new WebSocket(url)` | `Ws233Socket.Create(url, pool)` |
| `socket.ConnectAsync()` | `socket.Connect()` |
| `socket.SendAsync(bytes)` | `socket.Send(bytes)` 或 `socket.Send(span)` |
| `socket.SendAsync(str)` | `socket.SendText(str)` |
| `socket.CloseAsync()` | `socket.Close()` |
| `socket.OnOpen += ...` | `socket.Opened += ...` |
| `socket.OnMessage += (sender, e) => e.RawData` | `socket.BinaryReceived += frame => { frame.Span; frame.Release(); }` |
| `socket.OnError += (sender, e) => e.Message` | `socket.Errored += msg => ...` |
| `socket.OnClose += (sender, e) => e.Code` | `socket.Closed += (code, reason) => ...` |
| `socket.ReadyState` | `socket.ReadyState`（枚举名 `Ws233State`） |

---

## 迁移示例

### Before（UnityWebSocket）

```csharp
using UnityWebSocket;

var socket = new WebSocket("wss://game.example/ws");
socket.OnOpen += (_, __) => socket.SendAsync(_loginBytes);
socket.OnMessage += (_, e) =>
{
    Handle(e.RawData);
};
socket.ConnectAsync();
```

### After（unity233-websocket）

```csharp
using Unity233.WebSocket;

var pool = new Ws233BufferPool();
var socket = Ws233Socket.Create("wss://game.example/ws", pool);
socket.Opened += () => socket.Send(_loginBytes);
socket.BinaryReceived += frame =>
{
    try { Handle(frame.Span); }
    finally { frame.Release(); }
};
socket.Connect();
```

---

## Text 消息迁移

若此前依赖 Text 帧：

```csharp
// UnityWebSocket
socket.OnMessage += (_, e) =>
{
    if (e.IsText) Debug.Log(e.Data);
};
```

本库 **不主推 Text 接收**。建议：

1. 服务端改为 Binary 帧（推荐），或
2. 临时在 Native 层自行用 UTF8 解 `frame.Span`（仍比 string 事件少一层包装）

---

## 性能验证清单

迁移后在 WebGL 构建上确认：

- [ ] 所有 `BinaryReceived` 分支都 `Release()`
- [ ] 热路径无 `ToArray()` / `GetString()`
- [ ] Profiler GC Alloc 随消息量不再线性上涨
- [ ] `Application.runInBackground = true`

---

## 回滚

保留原 manifest 依赖即可回滚；业务 API 差异集中在事件签名与 `Release()` 契约。
