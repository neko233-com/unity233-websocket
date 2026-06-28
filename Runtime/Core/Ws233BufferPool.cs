using System;
using System.Collections.Generic;

namespace Unity233.WebSocket
{
    /// <summary>
    /// Power-of-two bucket pool for receive buffers. Rent/Return to avoid per-message GC on hot paths.
    /// </summary>
    public sealed class Ws233BufferPool
    {
        readonly Dictionary<int, Stack<byte[]>> _buckets = new();
        readonly int _maxPerBucket;

        public Ws233BufferPool(int maxPerBucket = 32)
        {
            _maxPerBucket = maxPerBucket;
        }

        public byte[] Rent(int minimumLength)
        {
            var size = NextPowerOfTwo(minimumLength);
            if (_buckets.TryGetValue(size, out var stack) && stack.Count > 0)
            {
                return stack.Pop();
            }

            return new byte[size];
        }

        public void Return(byte[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
            {
                return;
            }

            var size = buffer.Length;
            if (!IsPowerOfTwo(size))
            {
                return;
            }

            if (!_buckets.TryGetValue(size, out var stack))
            {
                stack = new Stack<byte[]>(_maxPerBucket);
                _buckets[size] = stack;
            }

            if (stack.Count < _maxPerBucket)
            {
                stack.Push(buffer);
            }
        }

        static int NextPowerOfTwo(int value)
        {
            if (value <= 256)
            {
                return 256;
            }

            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value + 1;
        }

        static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;
    }
}
