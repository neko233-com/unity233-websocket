# 与 UnityWebSocket 对比

对标项目：[psygames/UnityWebSocket](https://github.com/psygames/UnityWebSocket)（Unity 社区使用最广的 WebSocket 插件之一）。

## 设计目标差异

| 维度 | UnityWebSocket | unity233-websocket |
|------|----------------|---------------------|
| 首要目标 | 全平台覆盖、API 易用、生态成熟 | WebGL 热路径 **低延迟 + 近零 GC** |
| 消息模型 | Text + Binary 一等公民 | **Binary-first**（Text 仅作调试辅助） |
| 事件模型 | `EventHandler<T>` + 包装类 | 轻量 `Action` + `struct` 消息 |
| 异步模型 | `ConnectAsync` / `SendAsync` 命名 | WebGL 同步 JSLIB 调用，无 `Task` 开销 |
| 依赖 | 无第三方运行时依赖 | 无第三方运行时依赖 |

## 功能对照

| 功能 | UnityWebSocket | unity233-websocket | 备注 |
|------|:--------------:|:------------------:|------|
| WebGL | ✅ | ✅ | 均通过 Emscripten JSLIB |
| Editor 调试 | ✅（走 WebGL 或 Native 分支） | ✅（Editor 走 Native） | |
| Standalone / Mobile | ✅ | ✅（`ClientWebSocket`） | |
| 子协议 SubProtocol | ✅ | ✅ | |
| Unity 6 dynCall | ✅ | ✅ | |
| Text 消息收发 | ✅ | ⚠️ `SendText` 可用，接收不主推 | 游戏协议建议全 Binary |
| 在线 Demo / 工具菜单 | ✅ | ❌ 暂无 | Roadmap |
| 微信小游戏 | ✅ 分支维护 | 📋 计划 | Roadmap |

## 性能相关对照（核心）

| 环节 | UnityWebSocket 实现 | unity233-websocket 实现 | 预期收益 |
|------|---------------------|-------------------------|----------|
| **WebGL 发送** | `HEAPU8.buffer.slice(ptr, ptr+len)` 再 `send` | `new Uint8Array(HEAPU8.buffer, ptr, len)` 再 `send` | 消除每次发送的 JS ArrayBuffer 拷贝 |
| **WebGL 接收（C#）** | `new byte[msgSize]` + `Marshal.Copy` | `BufferPool.Rent` + `Marshal.Copy` + `Release()` | 稳态下复用 `byte[]`，减少 GC 压力 |
| **WebGL 接收（JS）** | `_malloc` → 写入 → 回调 → `_free` | 同上（**尚未优化**） | 下一版 ring buffer 目标消除 |
| **事件参数** | `MessageEventArgs`（class，持 `byte[]`） | `WsBinaryMessage`（readonly struct） | 减少小对象与隐式持有 |
| **Native 接收** | 依平台实现 | 固定 8KB scratch + pool 输出 | 避免 receive 循环内重复分配 |

> 说明：接收链路仍有一次 **JS heap → WASM heap** 的不可避免拷贝（浏览器 `ArrayBuffer` 与 Unity WASM 内存隔离）。本库优化的是 **拷贝之后的 C# 侧分配**，以及 **发送侧 JS 的额外 slice**。

## 何时选 UnityWebSocket

- 需要成熟的全平台方案、在线 Demo、Editor 工具菜单
- 协议大量依赖 **Text 帧**（JSON 字符串等）
- 消息频率低，GC 不是瓶颈
- 需要微信小游戏等已维护的特殊分支

## 何时选 unity233-websocket

- WebGL 多人 / 实时对战，**每秒数十～数百条** Binary 帧
- 已使用自定义二进制协议（Protobuf、FlatBuffers、自研帧格式）
- 希望在 Profiler 里看到 **WebGL 主线程 GC 接近 0**
- 愿意遵循 [零 GC 使用指南](zero-gc-guide.md)（显式 `Release()`）

## 架构对比（简图）

```mermaid
flowchart LR
    subgraph UW["UnityWebSocket 发送"]
        A1[C# byte[]] --> A2[JSLIB]
        A2 --> A3["slice() 新 ArrayBuffer"]
        A3 --> A4[browser WebSocket.send]
    end

    subgraph U233["unity233-websocket 发送"]
        B1[C# pooled byte[]] --> B2[JSLIB]
        B2 --> B3["Uint8Array view"]
        B3 --> B4[browser WebSocket.send]
    end
```

```mermaid
flowchart LR
    subgraph UW2["UnityWebSocket 接收"]
        R1[JS onmessage] --> R2[_malloc + copy]
        R2 --> R3[C# new byte[]]
        R3 --> R4[OnMessage event]
    end

    subgraph U2332["unity233-websocket 接收"]
        S1[JS onmessage] --> S2[_malloc + copy]
        S2 --> S3[C# BufferPool.Rent]
        S3 --> S4[BinaryReceived]
        S4 --> S5[Release 回池]
    end
```

## 诚实边界：我们**还没有**比 UnityWebSocket 更好的地方

以下环节与 UnityWebSocket **相同或尚未超越**，文档中不夸大：

1. **JS 接收临时缓冲**：仍使用 `_malloc` / `_free`（见 [optimizations.md](optimizations.md#receive-path-current)）
2. **字符串事件**：`Opened` / `Closed` / `Errored` 仍可能产生 string GC（低频，可接受）
3. **生态**：无 Releases 打包、无在线 WebGL Demo 页
4. **Text 二进制双通道**：UnityWebSocket 对 Text 接收是一等支持；本库聚焦 Binary

这些已列入 Roadmap，会在后续版本逐项消化。
