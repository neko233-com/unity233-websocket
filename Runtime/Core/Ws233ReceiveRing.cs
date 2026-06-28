using System;

namespace Unity233.WebSocket
{
    /// <summary>
    /// Fixed-slot receive ring backed by one WASM-visible byte[]. JS writes frames in-place; no _malloc per message.
    /// </summary>
    public sealed class Ws233ReceiveRing
    {
        readonly byte[] _backing;
        readonly int _slotSize;
        readonly int _slotCount;
        readonly int _flagsOffset;

        public Ws233ReceiveRing(int slotCount = 32, int slotSize = 4096)
        {
            if (slotCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(slotCount));
            }

            if (slotSize < 64)
            {
                throw new ArgumentOutOfRangeException(nameof(slotSize));
            }

            _slotCount = slotCount;
            _slotSize = slotSize;
            _flagsOffset = slotCount * slotSize;
            _backing = new byte[_flagsOffset + slotCount];
        }

        public byte[] Backing => _backing;

        public int SlotSize => _slotSize;

        public int SlotCount => _slotCount;

        public int FlagsOffset => _flagsOffset;

        public int TotalByteLength => _backing.Length;

        public int GetSlotOffset(int slotIndex) => slotIndex * _slotSize;

        public ArraySegment<byte> GetSegment(int slotIndex, int count)
        {
            return new ArraySegment<byte>(_backing, GetSlotOffset(slotIndex), count);
        }

        public void ReleaseSlot(int slotIndex)
        {
            _backing[_flagsOffset + slotIndex] = 0;
        }
    }
}
