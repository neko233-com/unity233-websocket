# 零 GC 使用指南

unity233-websocket 的池化与零拷贝发送是 **opt-in 契约**：框架提供能力，业务必须配合才能稳定达到近零 GC。

---

## 黄金规则

1. **一个连接 / 一个逻辑会话 → 一个 `Ws233BufferPool`**
2. **每条 `BinaryReceived` → 必须 `frame.Release()`**（包括 early return / exception 路径）
3. **热路径只用 Binary**，避免 `SendText` 与 `Encoding.UTF8.GetString`
4. **发送侧**优先 `ReadOnlySpan<byte>` + 自管缓冲或 pool scratch

---

## 推荐模式

### 连接生命周期

```csharp
public sealed class NetSession : IDisposable
{
    readonly Ws233BufferPool _pool = new(maxPerBucket: 64);
    readonly IWs233Socket _socket;

    public NetSession(string url)
    {
        _socket = Ws233Socket.Create(url, _pool);
        _socket.BinaryReceived += OnBinary;
        _socket.Connect();
    }

    void OnBinary(WsBinaryMessage frame)
    {
        try
        {
            ParseFrame(frame.Span);
        }
        finally
        {
            frame.Release();
        }
    }

    public void SendFrame(ReadOnlySpan<byte> payload) => _socket.Send(payload);

    public void Dispose() => _socket.Dispose();
}
```

### 解析时不拷贝（理想）

```csharp
void ParseFrame(ReadOnlySpan<byte> data)
{
    var opcode = data[0];
    var body = data.Slice(1);
    // 直接在 Span 上读，勿 data.ToArray()
}
```

### 必须缓存时

若协议层需要把 payload 存进队列，应：

- 拷贝到 **自己的 pool 或 NativeArray / 固定缓冲**，或
- 延长 `WsBinaryMessage` 生命周期并在出队后 `Release()`

**不要** `frame.Span.ToArray()` 除非刻意接受分配。

---

## 发送侧

### ✅ 推荐

```csharp
// 栈上小帧
Span<byte> buf = stackalloc byte[16];
buf[0] = 0x01;
socket.Send(buf);

// 复用成员缓冲
_payload[0] = opcode;
socket.Send(_payload.AsSpan(0, length));
```

### ⚠️ 可接受（有一次 CopyTo scratch）

```csharp
socket.Send(largeRentedBuffer.AsSpan(0, len)); // 内部 pool scratch + JS zero-copy send
```

### ❌ 热路径避免

```csharp
socket.SendText(json);                    // string + UTF8 分配
socket.Send(Encoding.UTF8.GetBytes(s));   // 每次 new byte[]
```

---

## Pool 参数

```csharp
new Ws233BufferPool(maxPerBucket: 64)
```

| 参数 | 含义 |
|------|------|
| `maxPerBucket` | 每个尺寸桶最多缓存多少块；并发 in-flight 消息多时应加大 |

池按 **2 的幂** 取整（≥256）。例如 300 字节的帧会租用 512 字节的块。

若 `Release()` 不及时，池外仍存活的消息块会导致 `Rent` 继续 `new byte[]`——表现为 GC 回升。

---

## WebGL 特别注意

```csharp
void Awake()
{
    Application.runInBackground = true;
}
```

浏览器 tab 失焦时 Unity 可能暂停主循环，WebSocket 回调堆积；与 UnityWebSocket 相同建议。

---

## 常见泄漏场景

| 场景 | 后果 |
|------|------|
| 忘记 `Release()` | pool 耗尽 → 持续 new byte[] |
| 在 async 回调里持有 `WsBinaryMessage` 不释放 | 同上 |
| `BinaryReceived += frame => Queue.Enqueue(frame)` 未出队 Release | 内存与池泄漏 |

使用 `try/finally { frame.Release(); }` 是最稳妥的默认写法。

---

## Editor vs WebGL

Editor 走 `Ws233NativeSocket`，但 **Release 契约相同**。在 Editor 用 Profiler 验证逻辑，再扫 WebGL 构建确认 GC。
