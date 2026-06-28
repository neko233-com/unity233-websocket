#if !UNITY_EDITOR && UNITY_WEBGL
using System;
using System.Runtime.InteropServices;
using AOT;

namespace Unity233.WebSocket
{
    internal static class Ws233WebGLBridge
    {
        static Ws233WebGLSocket[] _sockets = new Ws233WebGLSocket[8];
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
            var instanceId = socket.InstanceId;
            if (instanceId < 0)
            {
                return;
            }

            if (instanceId >= _sockets.Length)
            {
                Array.Resize(ref _sockets, NextCapacity(instanceId + 1));
            }

            _sockets[instanceId] = socket;
        }

        public static void Untrack(int instanceId)
        {
            if ((uint)instanceId < (uint)_sockets.Length)
            {
                _sockets[instanceId] = null;
            }
        }

        static Ws233WebGLSocket GetSocket(int instanceId)
        {
            return (uint)instanceId < (uint)_sockets.Length ? _sockets[instanceId] : null;
        }

        static int NextCapacity(int min)
        {
            var capacity = _sockets.Length;
            while (capacity < min)
            {
                capacity *= 2;
            }

            return capacity;
        }

        [MonoPInvokeCallback(typeof(OnOpenCallback))]
        static void DelegateOnOpen(int instanceId)
        {
            var socket = GetSocket(instanceId);
            if (socket != null)
            {
                socket.HandleOpen();
            }
        }

        [MonoPInvokeCallback(typeof(OnMessageRingCallback))]
        static void DelegateOnMessageRing(int instanceId, int slotIndex, int msgSize)
        {
            var socket = GetSocket(instanceId);
            if (socket != null)
            {
                socket.HandleBinaryFromRing(slotIndex, msgSize);
            }
        }

        [MonoPInvokeCallback(typeof(OnMessageCallback))]
        static void DelegateOnMessagePooled(int instanceId, IntPtr msgPtr, int msgSize)
        {
            var socket = GetSocket(instanceId);
            if (socket == null)
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
            var socket = GetSocket(instanceId);
            if (socket != null)
            {
                socket.HandleError(Marshal.PtrToStringAuto(errorPtr));
            }
        }

        [MonoPInvokeCallback(typeof(OnCloseCallback))]
        static void DelegateOnClose(int instanceId, int closeCode, IntPtr reasonPtr)
        {
            var socket = GetSocket(instanceId);
            if (socket != null)
            {
                socket.HandleClose((ushort)closeCode, Marshal.PtrToStringAuto(reasonPtr));
            }
        }
    }
}
#endif
