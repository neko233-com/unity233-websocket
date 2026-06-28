#if !UNITY_EDITOR && UNITY_WEBGL
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;

namespace Unity233.WebSocket
{
    internal static class Ws233WebGLBridge
    {
        static readonly Dictionary<int, Ws233WebGLSocket> Sockets = new();
        static bool _initialized;

        public delegate void OnOpenCallback(int instanceId);
        public delegate void OnMessageCallback(int instanceId, IntPtr msgPtr, int msgSize);
        public delegate void OnMessageRingCallback(int instanceId, int slotIndex, int msgSize);
        public delegate void OnErrorCallback(int instanceId, IntPtr errorPtr);
        public delegate void OnCloseCallback(int instanceId, int closeCode, IntPtr reasonPtr);

        [DllImport("__Internal")] public static extern int WebSocket233Connect(int instanceId);
        [DllImport("__Internal")] public static extern int WebSocket233Close(int instanceId, int code, string reason);
        [DllImport("__Internal")] public static extern int WebSocket233Send(int instanceId, byte[] data, int dataLength);
        [DllImport("__Internal")] public static extern int WebSocket233SendStr(int instanceId, string data);
        [DllImport("__Internal")] public static extern int WebSocket233GetState(int instanceId);
        [DllImport("__Internal")] public static extern int WebSocket233Allocate(string url);
        [DllImport("__Internal")] public static extern int WebSocket233AddSubProtocol(int instanceId, string protocol);
        [DllImport("__Internal")] public static extern void WebSocket233Free(int instanceId);
        [DllImport("__Internal")] public static extern int WebSocket233BindReceiveRing(int instanceId, byte[] backing, int slotSize, int slotCount, int flagsOffset);
        [DllImport("__Internal")] public static extern void WebSocket233SetOnOpen(OnOpenCallback callback);
        [DllImport("__Internal")] public static extern void WebSocket233SetOnMessage(OnMessageCallback callback);
        [DllImport("__Internal")] public static extern void WebSocket233SetOnMessageRing(OnMessageRingCallback callback);
        [DllImport("__Internal")] public static extern void WebSocket233SetOnError(OnErrorCallback callback);
        [DllImport("__Internal")] public static extern void WebSocket233SetOnClose(OnCloseCallback callback);
        [DllImport("__Internal")] public static extern void WebSocket233SetSupport6000();

        static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            WebSocket233SetOnOpen(DelegateOnOpen);
            WebSocket233SetOnMessage(DelegateOnMessagePooled);
            WebSocket233SetOnMessageRing(DelegateOnMessageRing);
            WebSocket233SetOnError(DelegateOnError);
            WebSocket233SetOnClose(DelegateOnClose);
#if UNITY_6000_0_OR_NEWER
            WebSocket233SetSupport6000();
#endif
            _initialized = true;
        }

        public static int Allocate(string address)
        {
            EnsureInitialized();
            return WebSocket233Allocate(address);
        }

        public static void BindReceiveRing(int instanceId, Ws233ReceiveRing ring)
        {
            EnsureInitialized();
            var code = WebSocket233BindReceiveRing(
                instanceId,
                ring.Backing,
                ring.SlotSize,
                ring.SlotCount,
                ring.FlagsOffset);

            if (code < 0)
            {
                throw new InvalidOperationException($"Failed to bind receive ring: {code}");
            }
        }

        public static void Track(Ws233WebGLSocket socket)
        {
            if (!Sockets.ContainsKey(socket.InstanceId))
            {
                Sockets.Add(socket.InstanceId, socket);
            }
        }

        public static void Untrack(int instanceId)
        {
            Sockets.Remove(instanceId);
        }

        [MonoPInvokeCallback(typeof(OnOpenCallback))]
        static void DelegateOnOpen(int instanceId)
        {
            if (Sockets.TryGetValue(instanceId, out var socket))
            {
                socket.HandleOpen();
            }
        }

        [MonoPInvokeCallback(typeof(OnMessageRingCallback))]
        static void DelegateOnMessageRing(int instanceId, int slotIndex, int msgSize)
        {
            if (Sockets.TryGetValue(instanceId, out var socket))
            {
                socket.HandleBinaryFromRing(slotIndex, msgSize);
            }
        }

        [MonoPInvokeCallback(typeof(OnMessageCallback))]
        static void DelegateOnMessagePooled(int instanceId, IntPtr msgPtr, int msgSize)
        {
            if (!Sockets.TryGetValue(instanceId, out var socket))
            {
                return;
            }

            var rented = socket.BufferPool.Rent(msgSize);
            Marshal.Copy(msgPtr, rented, 0, msgSize);
            socket.HandleBinaryPooled(rented, msgSize);
        }

        [MonoPInvokeCallback(typeof(OnErrorCallback))]
        static void DelegateOnError(int instanceId, IntPtr errorPtr)
        {
            if (Sockets.TryGetValue(instanceId, out var socket))
            {
                socket.HandleError(Marshal.PtrToStringAuto(errorPtr));
            }
        }

        [MonoPInvokeCallback(typeof(OnCloseCallback))]
        static void DelegateOnClose(int instanceId, int closeCode, IntPtr reasonPtr)
        {
            if (Sockets.TryGetValue(instanceId, out var socket))
            {
                socket.HandleClose((ushort)closeCode, Marshal.PtrToStringAuto(reasonPtr));
            }
        }
    }
}
#endif
