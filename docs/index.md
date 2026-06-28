# unity233-websocket 文档

面向 Unity WebGL 的**超低延迟、近零 GC** WebSocket 客户端。本库以 [psygames/UnityWebSocket](https://github.com/psygames/UnityWebSocket) 为参照实现，在保持相似使用体验的前提下，针对**高频收发**场景做了专项优化。

## 文档目录

| 文档 | 说明 |
|------|------|
| [为什么 WebGL 必须挂 JS？](webgl-jslib-explained.md) | jslib / `__Internal` 原理，能否去掉 JS |
| [与 UnityWebSocket 对比](comparison.md) | 功能对照、设计取舍、适用场景 |
| [优化原理详解](optimizations.md) | JSLIB / C# / 环形缓冲的具体改动 |
| [UniTask 支持](unitask.md) | 可选 async API 与安装方式 |
| [零 GC 使用指南](zero-gc-guide.md) | 业务侧如何写出稳定 0 GC 热路径 |
| [从 UnityWebSocket 迁移](migration.md) | API 对照与迁移步骤 |

**在线文档：** https://neko233-com.github.io/unity233-websocket/

## 快速链接

- 仓库：[github.com/neko233-com/unity233-websocket](https://github.com/neko233-com/unity233-websocket)
- 对标项目：[github.com/psygames/UnityWebSocket](https://github.com/psygames/UnityWebSocket)
- UPM 安装：

```text
https://github.com/neko233-com/unity233-websocket.git
```

## 一句话总结

**UnityWebSocket 解决「全平台能用」；unity233-websocket 解决「WebGL 上尽量别分配、尽量别多拷贝」。**

## 当前版本能力边界

已实现：

- WebGL JSLIB 桥接（Unity 2021.3+，含 Unity 6 `makeDynCall` 兼容）
- 发送路径零拷贝（JS 侧 `Uint8Array` view，无 `buffer.slice()`）
- **接收环形缓冲 `Ws233ReceiveRing`**（JS `HEAPU8.set` 直写 WASM，稳态无 `_malloc`）
- 接收降级：`BufferPool` + 显式 `Release()`
- **可选 UniTask 扩展**（`ConnectAsync` / `RunReceiveLoopAsync`）
- Editor / Standalone `ClientWebSocket` 回退
- Binary-first API（`ReadOnlySpan<byte>`）

规划中：

- SharedArrayBuffer / 批量 drain（进一步降 dynCall 频率）
- 压缩扩展协商
- 微信 / 团结小游戏 JSLIB 分支
- 与 UnityWebSocket / NativeWebSocket 的可复现 Benchmark
