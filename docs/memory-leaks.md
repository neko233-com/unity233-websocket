# UnityWebSocket JS 内存泄漏与 OOM：根因与本库修复

对标 [psygames/UnityWebSocket](https://github.com/psygames/UnityWebSocket)。微信小游戏 / 团结引擎 WebGL 上，**JS Heap 持续上涨直至 OOM** 是社区高频问题；本库从 JSLIB 机制层逐项消除已知泄漏源。

---

## 典型现象（微信小游戏）

- 对局长时间运行后，微信开发者工具 / 真机 **DynamicMemory** 单调上升
- iOS 低档机 1GB 上限下频繁 **OOM 重启**
- Unity Profiler 中 C# GC 不高，但 **JS 内存**（Performance 面板）持续涨

这往往 **不是** C# 泄漏，而是 **每条 WebSocket 消息在 JS 层分配、且 handler 闭包与 WebSocket 对象未释放** 叠加导致。

---

## UnityWebSocket 已知 JS 泄漏点

| # | 问题 | UnityWebSocket 行为 | 后果 |
|---|------|---------------------|------|
| 1 | **发送 `buffer.slice()`** | 每条发送 new ArrayBuffer | JS 堆线性增长（高频 send） |
| 2 | **接收 `_malloc` / `_free`** | 每条消息 malloc 临时缓冲 | emscripten 堆碎片 + JS 侧压力 |
| 3 | **Blob + FileReader 分支** | 异步 `reader.onload` 闭包 | 小游戏环境易滞留，闭包引用 `ev` |
| 4 | **Text 帧 `onMessageStr`** | 额外 UTF8 malloc 路径 | 双通道分配 |
| 5 | **Handler 未 detach** | close/free 后 `onmessage` 仍挂载 | WebSocket 与 WASM 回调无法 GC |
| 6 | **`ws !== null` 判断** | `undefined.readyState` 崩溃或异常路径 | 切 tab / 断网后实例状态错乱，实例表泄漏 |
| 7 | **实例表只增不减** | 异常 close 未 `delete instances[id]` | 每个连接残留 `{url, ws, ...}` |
| 8 | **C# 每包 `new byte[]`** | 热路径 WASM 堆涨 | UnityHeap 与 GC 压力（小游戏 500MB 预算极紧） |

---

## unity233-websocket 修复对照

| # | 本库做法 |
|---|----------|
| 1 | `Uint8Array(HEAPU8.buffer, ptr, len)` **零拷贝 send** |
| 2 | **`Ws233ReceiveRing`**：稳态 `HEAPU8.set` 直写 WASM，**无 per-msg malloc** |
| 3 | **移除 FileReader/Blob 分支**；强制 `binaryType = "arraybuffer"` |
| 4 | **Binary-only 接收**；无 `onMessageStr` |
| 5 | **`cleanupWebSocket`**：`onopen/onmessage/onerror/onclose = null` |
| 6 | 全面 **`ws == null`**（含 undefined）+ try/catch close |
| 7 | **`destroyInstance`**：清 ws、ring、subProtocols、url 后 `delete instances[id]` |
| 8 | **`BufferPool` + ring 槽位**；C# 热路径无 `new byte[]`（环形路径零 Marshal.Copy） |
| 9 | **降级路径复用 scratch** | `ensureDeliverScratch` 单块复用，非每包 `_malloc` |
| 10 | **错误/close 字符串复用** | `ensureStringScratch`，非每事件 malloc+free |
| 11 | **WeixinMiniGame 平台** | `.jslib.meta` 启用 `WeixinMiniGame`（与 UnityWebSocket 同级） |
| 12 | **Dispose 释放 ring 槽** | `ReleaseAllSlots()` 防止 flag 永久占用导致降级风暴 |

---

## 微信小游戏推荐配置

```csharp
var options = Ws233Options.WeChatMinigameDefaults;
options.BufferPool = new Ws233BufferPool(maxPerBucket: 48);
var socket = Ws233Socket.Create("wss://your-server/ws", options);
```

业务侧仍须：

1. **每条** `WsBinaryMessage.Release()`（否则 ring 槽占满 → 降级 → JS scratch 压力上升）
2. 连接结束 **`Dispose()`** 或显式 `Close()`
3. 避免 `SendText` / JSON 字符串热路径
4. 微信官方：控制 UnityHeap 预留、勿用 IndexedDB 文件缓存（见[微信内存优化文档](https://developers.weixin.qq.com/minigame/dev/guide/game-engine/unity-webgl-transform/Design/OptimizationMemory.html)）

---

## 如何验证「不再泄漏」

1. 微信开发者工具 → **性能面板** → 观察 **JavaScript 内存** 10 分钟高频收发
2. Unity WebGL → Chrome Performance → Memory：对比 UnityWebSocket vs 本库相同 QPS
3. 关注：**发送 N 次后 JS ArrayBuffer 计数是否仍线性增**（slice 泄漏的典型特征）

---

## 仍无法魔法消除的部分

- 浏览器 → WASM **至少 1 次** payload 拷贝（安全隔离）
- 低频 `Errored` / `Closed` 的 **string** 仍可能分配（事件级，非热路径）
- 业务层忘记 `Release()` 会导致 ring 占满——属于 **使用契约**，不是库泄漏

详见 [optimizations.md](optimizations.md)、[zero-gc-guide.md](zero-gc-guide.md)。
