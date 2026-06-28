namespace Unity233.WebSocket
{
    public static class Ws233Socket
    {
        public static IWs233Socket Create(string address, Ws233BufferPool bufferPool = null)
        {
            return Create(address, new Ws233Options { BufferPool = bufferPool });
        }

        public static IWs233Socket Create(string address, Ws233Options options)
        {
            options ??= new Ws233Options();
#if !UNITY_EDITOR && UNITY_WEBGL
            return new Ws233WebGLSocket(address, null, options);
#else
            return new Ws233NativeSocket(address, null, options);
#endif
        }

        public static IWs233Socket Create(string address, string subProtocol, Ws233BufferPool bufferPool = null)
        {
            return Create(address, new[] { subProtocol }, bufferPool);
        }

        public static IWs233Socket Create(string address, string[] subProtocols, Ws233BufferPool bufferPool = null)
        {
            return Create(address, subProtocols, new Ws233Options { BufferPool = bufferPool });
        }

        public static IWs233Socket Create(string address, string[] subProtocols, Ws233Options options)
        {
            options ??= new Ws233Options();
#if !UNITY_EDITOR && UNITY_WEBGL
            return new Ws233WebGLSocket(address, subProtocols, options);
#else
            return new Ws233NativeSocket(address, subProtocols, options);
#endif
        }
    }
}
