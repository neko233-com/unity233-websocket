using NUnit.Framework;

namespace Unity233.WebSocket.Tests
{
    public sealed class WsBinaryMessageTests
    {
        [Test]
        public void PooledMessage_ExposesRequestedWindowAndReturnsBufferOnRelease()
        {
            var pool = new Ws233BufferPool();
            var buffer = pool.Rent(512);
            buffer[10] = 42;
            buffer[11] = 43;
            var message = new WsBinaryMessage(buffer, offset: 10, count: 2, pool);

            Assert.That(message.Count, Is.EqualTo(2));
            Assert.That(message.IsFromRing, Is.False);
            Assert.That(message.Span[0], Is.EqualTo(42));
            Assert.That(message.Segment.Array, Is.SameAs(buffer));
            Assert.That(message.Segment.Offset, Is.EqualTo(10));
            Assert.That(message.Segment.Count, Is.EqualTo(2));

            message.Release();

            Assert.That(pool.Rent(512), Is.SameAs(buffer));
        }

        [Test]
        public void RingMessage_ExposesSlotWindowAndClearsFlagOnRelease()
        {
            var ring = new Ws233ReceiveRing(slotCount: 2, slotSize: 128);
            ring.Backing[128] = 7;
            ring.Backing[129] = 8;
            ring.Backing[ring.FlagsOffset + 1] = 1;
            var message = new WsBinaryMessage(ring, slotIndex: 1, count: 2);

            Assert.That(message.Count, Is.EqualTo(2));
            Assert.That(message.IsFromRing, Is.True);
            Assert.That(message.Span[0], Is.EqualTo(7));
            Assert.That(message.Span[1], Is.EqualTo(8));
            Assert.That(message.Segment.Array, Is.SameAs(ring.Backing));
            Assert.That(message.Segment.Offset, Is.EqualTo(128));

            message.Release();

            Assert.That(ring.Backing[ring.FlagsOffset + 1], Is.Zero);
        }

        [Test]
        public void DefaultMessage_IsSafeToRelease()
        {
            var message = default(WsBinaryMessage);

            Assert.That(message.Count, Is.Zero);
            Assert.DoesNotThrow(() => message.Release());
        }
    }
}
