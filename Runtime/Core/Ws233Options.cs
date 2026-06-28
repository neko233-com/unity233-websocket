namespace Unity233.WebSocket
{
    public sealed class Ws233Options
    {
        public Ws233BufferPool BufferPool { get; set; }

        /// <summary>WebGL receive ring slot count. Each slot holds one in-flight frame.</summary>
        public int ReceiveRingSlotCount { get; set; } = 32;

        /// <summary>Max binary frame bytes per ring slot; larger frames use reusable JS scratch (not per-msg malloc).</summary>
        public int ReceiveRingSlotSize { get; set; } = 4096;

        /// <summary>WeChat minigame preset: larger ring, smaller slots tuned for frequent small packets.</summary>
        public static Ws233Options WeChatMinigameDefaults => new()
        {
            ReceiveRingSlotCount = 48,
            ReceiveRingSlotSize = 2048,
        };
    }
}
