using System;

namespace Unity233.WebSocket
{
    /// <summary>
    /// Zero-copy friendly binary frame. Call <see cref="Release"/> when parsing is done.
    /// </summary>
    public readonly struct WsBinaryMessage
    {
        readonly Ws233BufferPool _pool;
        readonly byte[] _buffer;
        readonly int _offset;
        readonly int _count;

        public WsBinaryMessage(byte[] buffer, int offset, int count, Ws233BufferPool pool)
        {
            _buffer = buffer;
            _offset = offset;
            _count = count;
            _pool = pool;
        }

        public int Count => _count;

        public ReadOnlySpan<byte> Span => new(_buffer, _offset, _count);

        public ArraySegment<byte> Segment => new(_buffer, _offset, _count);

        public void Release()
        {
            _pool?.Return(_buffer);
        }
    }
}
