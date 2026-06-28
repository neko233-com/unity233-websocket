# 优化原理详解

本文说明 unity233-websocket 相对 [UnityWebSocket](https://github.com/psygames/UnityWebSocket) 在实现层做了哪些改动、为什么能降 GC / 降延迟，以及各层仍存在的成本。

---

## 1. WebGL 发送：消除 `buffer.slice()`

### UnityWebSocket 做法

`WebSocket.jslib` 中 `WebSocketSend` 大致为：

```javascript
instance.ws.send(HEAPU8.buffer.slice(bufferPtr, bufferPtr + length));
```

`ArrayBuffer.prototype.slice` 会**分配一块新的 ArrayBuffer** 并复制指定区间。每条发送消息都会在 JS 堆上产生一次额外分配 + 拷贝，高 QPS 下明显。

### unity233-websocket 做法

`WebSocket233.jslib` 中 `WebSocket233Send`：

```javascript
instance.ws.send(new Uint8Array(HEAPU8.buffer, bufferPtr, length));
```

`Uint8Array(buffer, byteOffset, length)` 只是在现有 WASM 内存上创建**视图**，不复制底层数据。浏览器 `WebSocket.send` 接受 `ArrayBufferView`，行为符合规范。

### C# 侧配合

`Ws233WebGLSocket.Send(ReadOnlySpan<byte>)` 从 `Ws233BufferPool` 租用 scratch，拷贝 payload 一次到 WASM 可见的 `byte[]`，再交给 JSLIB。scratch 在 `finally` 中归还 pool。

**成本模型（发送）**

| 步骤 | UnityWebSocket | unity233-websocket |
|------|----------------|---------------------|
| C# → WASM 数组 | 1 次（Unity 封送） | 1 次（显式 CopyTo scratch） |
| JS 额外分配 | 1 次 slice | **0** |
| 浏览器发出 | 1 次 | 1 次 |

---

## 2. WebGL 接收：对象池替代 `new byte[]`

### UnityWebSocket 做法

`WebSocketManager.DelegateOnMessageEvent`：

```csharp
var bytes = new byte[msgSize];
Marshal.Copy(msgPtr, bytes, 0, msgSize);
socket.HandleOnMessage(bytes);
```

每条消息至少分配一个与 payload 等长的 `byte[]`，并交给 `MessageEventArgs`。若业务层再拷贝或缓存，GC 压力叠加。

### unity233-websocket 做法

`Ws233WebGLBridge.DelegateOnMessage`：

```csharp
var rented = socket.BufferPool.Rent(msgSize);
Marshal.Copy(msgPtr, rented, 0, msgSize);
socket.HandleBinary(rented, msgSize);
```

`Ws233BufferPool` 按 **2 的幂** 分桶（最小 256），同尺寸缓冲复用。业务通过 `WsBinaryMessage` 暴露 `ReadOnlySpan<byte>` / `ArraySegment<byte>`，用完后 **`Release()`** 归还。

**成本模型（接收，C# 稳态）**

| 指标 | UnityWebSocket | unity233-websocket |
|------|----------------|---------------------|
| 每条消息 new byte[] | 是 | 否（池化） |
| 业务需归还缓冲 | 否（GC 回收） | 是（显式 Release） |
| 池耗尽时 | — | 仍会 new（降级，应调大 maxPerBucket） |

---

## 3. API 层：Binary-first 与 struct 消息

### 事件类型

- UnityWebSocket：`EventHandler<MessageEventArgs>`，`MessageEventArgs` 为 class
- unity233-websocket：`Action<WsBinaryMessage>`，`WsBinaryMessage` 为 **readonly struct**

struct 消息不单独堆分配；配合 pool 后，热路径上主要剩下 **delegate 调用本身** 的开销（与 UnityWebSocket 同级）。

### 命名与异步

WebGL 路径不使用 `async/await` / `Task`：

- 避免状态机分配
- JSLIB 回调直接在浏览器事件线程同步进入 C#（Unity 主线程调度与 UnityWebSocket 相同）

方法命名为 `Connect()` / `Send()` 而非 `*Async()`，表明 WebGL 上为**非 Task 驱动**。

---

## 4. Native / Editor 路径

`Ws233NativeSocket`（`#if UNITY_EDITOR || !UNITY_WEBGL`）：

| 优化点 | 说明 |
|--------|------|
| 固定 receive scratch | `byte[8192]` 成员，ReceiveAsync 循环内不重复分配 |
| 发送 pool scratch | 与 WebGL 相同，Send 租用 → SendAsync → Return |
| 输出仍走 pool | 拷贝到 `BufferPool.Rent(count)` 再抛 `WsBinaryMessage` |

保证 **Editor 压测与 WebGL 语义一致**（同样的 Release 契约）。

---

## 5. JSLIB 其它细节

| 改动 | 原因 |
|------|------|
| 默认 `binaryType = "arraybuffer"` | 避免 Blob 路径上的 FileReader 异步与额外分配 |
| 保留 Unity 6 `makeDynCall` 分支 | 与 UnityWebSocket 同级兼容 |
| 独立符号前缀 `WebSocket233*` | 可与 UnityWebSocket 共存于同一项目（不推荐，但符号不冲突） |

---

## 6. 接收路径：环形缓冲（v0.2+） {#receive-path-ring}

### 问题

JS `onmessage` 拿到的 `ArrayBuffer` 与 WASM heap **内存隔离**，至少要从 JS 侧拷贝一次。  
旧方案（UnityWebSocket 与本库 v0.1）：每条消息 `_malloc` → `writeArrayToMemory` → C# `Marshal.Copy` → `_free`。

### 本库 v0.2：`Ws233ReceiveRing`

连接前绑定一块 **预分配** 的 `byte[]`（C# `Ws233ReceiveRing`）：

```
[ slot0 | slot1 | ... | slotN-1 | flag0..flagN-1 ]
```

JS 收到帧时：

1. 找空闲 slot（flag == 0）
2. `HEAPU8.set(payload, ringBase + slot * slotSize)` — **无 `_malloc`**
3. `dynCall(onMessageRing, instanceId, slotIndex, length)`

C# `DelegateOnMessageRing` **直接** 从 ring 构造 `WsBinaryMessage`，**无 Marshal.Copy**。

降级（仍用池化 + `_malloc`）当：

- 帧长度 > `ReceiveRingSlotSize`（默认 4096）
- 所有 slot 未 `Release()`（in-flight 过多）

配置：

```csharp
var socket = Ws233Socket.Create(url, new Ws233Options
{
    ReceiveRingSlotCount = 32,
    ReceiveRingSlotSize = 4096,
});
```

### 成本模型（接收，环形稳态）

| 步骤 | UnityWebSocket | unity233-websocket v0.2 ring |
|------|----------------|------------------------------|
| JS 临时分配 | `_malloc` / `_free` | **无** |
| JS → WASM 拷贝 | 1 次 | 1 次（`HEAPU8.set`，不可避免） |
| C# 分配 | `new byte[]` | **无**（槽位已预分配） |
| C# 拷贝 | `Marshal.Copy` | **无** |

---

## 7. 如何验证优化有效

建议在目标 WebGL 构建上：

1. 打开 **Unity Profiler → GC Alloc**
2. 对比相同 QPS 下 UnityWebSocket vs unity233-websocket
3. 关注指标：
   - 每帧 GC Alloc（B/frame）
   - 发送/接收回调次数与分配堆栈

预期：在遵循 [zero-gc-guide.md](zero-gc-guide.md) 时，**Binary 热路径 GC 应接近 0**；UnityWebSocket 通常仍可见随消息数线性增长的 `byte[]` 分配。

Benchmark 场景列入 Roadmap，欢迎 PR。
