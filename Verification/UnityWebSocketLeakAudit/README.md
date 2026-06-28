# UnityWebSocket WebGL Leak Audit

This folder is intentionally separate from the package unit tests. It is for auditing the upstream
UnityWebSocket WebGL bridge and explaining why some versions are leak-prone on WebGL / WeChat minigame.

## What To Audit

Point the script at the upstream `WebSocket.jslib` file:

```powershell
powershell -ExecutionPolicy Bypass -File .\Verification\UnityWebSocketLeakAudit\UnityWebSocketLeakAudit.ps1 -Path C:\path\to\WebSocket.jslib
```

The audit checks for these high-risk patterns:

| Risk | Why it matters |
|------|----------------|
| `HEAPU8.buffer.slice(...)` before `WebSocket.send` | Allocates a new `ArrayBuffer` for every send. |
| Per-message `_malloc` / `_free` receive bridge | Creates Emscripten heap churn and fragmentation pressure. |
| `Blob` / `FileReader` receive path | Keeps asynchronous closures alive and can retain event payloads longer on minigame runtimes. |
| Text receive callback path | Adds UTF8 string allocation paths on top of binary delivery. |
| Missing handler detach | `onmessage` / `onclose` closures can keep the `WebSocket` instance and WASM callbacks reachable. |
| Missing instance deletion | Reconnect loops can leave `{ id, url, ws, callbacks }` objects in the JS instance table. |

## Expected Interpretation

A positive finding is not proof of an immediate OOM by itself. It is evidence of a per-message or
per-connection allocation path that becomes dangerous under high message rates, long sessions,
background pauses, or frequent reconnects. The strongest validation is:

1. Run this audit against the upstream `WebSocket.jslib`.
2. Build the same scene with upstream UnityWebSocket and this package.
3. In Chrome or WeChat DevTools, record JS heap / DynamicMemory under the same QPS for 10+ minutes.
4. Confirm whether `ArrayBuffer` count, JS heap, or DynamicMemory grows linearly with messages.
