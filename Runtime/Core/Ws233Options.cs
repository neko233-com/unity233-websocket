namespace Unity233.WebSocket
{
    public sealed class Ws233Options
    {
        public Ws233BufferPool BufferPool { get; set; }

        /// <summary>WebGL receive ring slot count. Each slot holds one in-flight frame.</summary>
        public int ReceiveRingSlotCount { get; set; } = 32;

        /// <summary>Max binary frame bytes per ring slot; larger frames fall back to pooled malloc path.</summary>
        public int ReceiveRingSlotSize { get; set; } = 4096;
    }
}
