using System;

namespace Unity233.WebSocket
{
    /// <summary>
    /// Binary frame from pool or WebGL receive ring. Always call <see cref="Release"/> when done.
    /// </summary>
    public readonly struct WsBinaryMessage
    {
        readonly Ws233BufferPool _pool;
        readonly Ws233ReceiveRing _ring;
        readonly byte[] _buffer;
        readonly int _offset;
        readonly int _count;
        readonly int _slotIndex;

        public WsBinaryMessage(byte[] buffer, int offset, int count, Ws233BufferPool pool)
        {
            _buffer = buffer;
            _offset = offset;
            _count = count;
            _pool = pool;
            _ring = null;
            _slotIndex = -1;
        }

        internal WsBinaryMessage(Ws233ReceiveRing ring, int slotIndex, int count)
        {
            _ring = ring;
            _slotIndex = slotIndex;
            _buffer = ring.Backing;
            _offset = ring.GetSlotOffset(slotIndex);
            _count = count;
            _pool = null;
        }

        public int Count => _count;

        public bool IsFromRing => _ring != null;

        public ReadOnlySpan<byte> Span => new(_buffer, _offset, _count);

        public ArraySegment<byte> Segment => new(_buffer, _offset, _count);

        public void Release()
        {
            if (_pool != null)
            {
                _pool.Return(_buffer);
            }
            else if (_ring != null)
            {
                _ring.ReleaseSlot(_slotIndex);
            }
        }
    }
}
