namespace Unity233.WebSocket
{
    public static class Ws233Socket
    {
        public static IWs233Socket Create(string address, Ws233BufferPool bufferPool = null)
        {
#if !UNITY_EDITOR && UNITY_WEBGL
            return new Ws233WebGLSocket(address, bufferPool);
#else
            return new Ws233NativeSocket(address, bufferPool);
#endif
        }

        public static IWs233Socket Create(string address, string subProtocol, Ws233BufferPool bufferPool = null)
        {
#if !UNITY_EDITOR && UNITY_WEBGL
            return new Ws233WebGLSocket(address, new[] { subProtocol }, bufferPool);
#else
            return new Ws233NativeSocket(address, new[] { subProtocol }, bufferPool);
#endif
        }

        public static IWs233Socket Create(string address, string[] subProtocols, Ws233BufferPool bufferPool = null)
        {
#if !UNITY_EDITOR && UNITY_WEBGL
            return new Ws233WebGLSocket(address, subProtocols, bufferPool);
#else
            return new Ws233NativeSocket(address, subProtocols, bufferPool);
#endif
        }
    }
}
