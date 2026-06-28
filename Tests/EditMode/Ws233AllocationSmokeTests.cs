using NUnit.Framework;

namespace Unity233.WebSocket.Tests
{
    public sealed class Ws233AllocationSmokeTests
    {
        [Test]
        public void BufferPool_SteadyStateRentReturnReusesSameBucketBuffer()
        {
            var pool = new Ws233BufferPool(maxPerBucket: 8);
            var warm = pool.Rent(1024);
            pool.Return(warm);

            for (var i = 0; i < 1000; i++)
            {
                var buffer = pool.Rent(1000);
                Assert.That(buffer, Is.SameAs(warm));
                pool.Return(buffer);
            }
        }

        [Test]
        public void ReceiveRing_ReleasingSlotsKeepsFlagsReusable()
        {
            var ring = new Ws233ReceiveRing(slotCount: 4, slotSize: 128);

            for (var i = 0; i < 1000; i++)
            {
                var slot = i % ring.SlotCount;
                ring.Backing[ring.FlagsOffset + slot] = 1;
                ring.ReleaseSlot(slot);
                Assert.That(ring.Backing[ring.FlagsOffset + slot], Is.Zero);
            }
        }
    }
}
