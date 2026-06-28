# 为什么 WebGL 必须「挂 JS」？能否去掉？

UnityWebSocket、NativeWebSocket、本库在 **WebGL 目标** 下都采用 `Plugins/WebGL/*.jslib` + `[DllImport("__Internal")]`。这不是某个库的「设计缺陷」，而是 **Unity WebGL 运行模型的硬约束**。

---

## 1. WebGL 里没有真正的 Socket

| 平台 | 能否 `new Socket()` / `ClientWebSocket` 直连 |
|------|---------------------------------------------|
| Windows / macOS / Android / iOS | ✅ 可以 |
| **WebGL（浏览器内 WASM）** | ❌ **不可以** |

Unity WebGL 构建产物是 **Emscripten WASM**，跑在浏览器沙箱里：

- 没有 BSD socket API
- 不能打开 TCP 连接
- 不能绕过浏览器安全模型

浏览器唯一标准的实时双向通道是 **`window.WebSocket`**（或 WebRTC 等更高层 API）。  
因此 C# 必须 **调用 JavaScript**，由 JS 创建 `WebSocket` 对象并收发帧。

Unity 官方文档：[WebGL networking](https://docs.unity3d.com/Manual/webgl-networking.html) 也明确说明 WebGL 不支持 .NET 套接字，需用 JS 插件。

---

## 2. `jslib` 不是「外挂 DLL」，是 Emscripten 官方桥接

```
┌─────────────┐    DllImport("__Internal")     ┌──────────────┐
│  C# / WASM  │ ◄────────────────────────────► │  .jslib      │
│  (IL2CPP)   │    dynCall / makeDynCall       │  (合并进      │
└─────────────┘                                │   runtime)   │
                                               └──────┬───────┘
                                                      │
                                               ┌──────▼───────┐
                                               │ Browser      │
                                               │ WebSocket API│
                                               └──────────────┘
```

- **`.jslib`**：构建时 `mergeInto(LibraryManager.library)` 编入 Emscripten 运行时
- **`__Internal`**：链接到同一 WASM 模块内的 JS 导出函数，不是操作系统 DLL
- **`dynCall`**：从 JS 回调 C# 静态 `[MonoPInvokeCallback]` 方法

UnityWebSocket 的 `WebSocket.jslib` 与本库的 `WebSocket233.jslib` 是 **同一类机制**，区别在 **桥接内部如何实现**（拷贝次数、分配次数）。

---

## 3. 能否不用 JS，纯 C# WebSocket？

在 **WebGL 发布** 下：**不能**（除非改用 WebTransport/WebRTC 等仍要 JS 的方案）。

可选误解澄清：

| 方案 | 是否免 JS |
|------|-----------|
| `System.Net.WebSockets` 仅 Editor/Standalone | WebGL 构建不会编译此路径 |
| 第三方「纯 C#」声称支持 WebGL | 底层仍含 jslib，只是封装隐藏 |
| HTTP 长轮询 | 不用 WebSocket，但延迟与开销更大 |

本库 **Editor / 非 WebGL** 使用 `ClientWebSocket`，**零 jslib**；只有 **WebGL 包** 才加载 `WebSocket233.jslib`。

---

## 4. 机制上能优化什么？（本库已做 / 计划）

JS 桥无法删除，但 **桥内每次操作的分配与拷贝** 可以优化。

### ✅ 已优化

| 环节 | 做法 |
|------|------|
| **发送** | `Uint8Array(HEAPU8.buffer, ptr, len)` 替代 `buffer.slice()` |
| **接收（稳态）** | `Ws233ReceiveRing`：JS `HEAPU8.set` 直写 WASM 环形槽，**无 `_malloc`** |
| **接收（降级）** | 帧大于 slot、或 ring 满 → 回退 `_malloc` + `BufferPool` |
| **C# 热路径** | 环形槽上 **零 Marshal.Copy**；池化路径仅降级时使用 |

### 📋 仍可继续优化

- 与浏览器 **SharedArrayBuffer** 协作（需 COOP/COEP 头，部署复杂）
- 批量 drain 环形缓冲，降低 dynCall 频率
- WebAssembly.Table / Unity 6 回调路径微调

---

## 5. 与 UnityWebSocket 的本质关系

- **相同**：都必须 jslib + `__Internal` + 浏览器 `WebSocket`
- **不同**：jslib 内部数据路径、C# 缓冲策略、API 面向 Binary / 零 GC

若目标是 WebGL **去掉 JS**，在现有 Web 标准下 **不可行**；若目标是 **同样用 JS 但更少分配、更低延迟**，正是本库方向。

详见 [optimizations.md](optimizations.md)、[comparison.md](comparison.md)。
