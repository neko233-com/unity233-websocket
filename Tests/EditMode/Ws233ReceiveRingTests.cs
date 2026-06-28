using NUnit.Framework;

namespace Unity233.WebSocket.Tests
{
    public sealed class Ws233ReceiveRingTests
    {
        [Test]
        public void Constructor_RejectsInvalidShape()
        {
            Assert.That(() => new Ws233ReceiveRing(0, 64), Throws.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => new Ws233ReceiveRing(1, 63), Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void Constructor_CalculatesBackingAndOffsets()
        {
            var ring = new Ws233ReceiveRing(slotCount: 3, slotSize: 128);

            Assert.That(ring.SlotCount, Is.EqualTo(3));
            Assert.That(ring.SlotSize, Is.EqualTo(128));
            Assert.That(ring.FlagsOffset, Is.EqualTo(384));
            Assert.That(ring.TotalByteLength, Is.EqualTo(387));
            Assert.That(ring.GetSlotOffset(0), Is.EqualTo(0));
            Assert.That(ring.GetSlotOffset(2), Is.EqualTo(256));
        }

        [Test]
        public void GetSegment_ReturnsExpectedBackingWindow()
        {
            var ring = new Ws233ReceiveRing(slotCount: 2, slotSize: 128);

            var segment = ring.GetSegment(slotIndex: 1, count: 7);

            Assert.That(segment.Array, Is.SameAs(ring.Backing));
            Assert.That(segment.Offset, Is.EqualTo(128));
            Assert.That(segment.Count, Is.EqualTo(7));
        }

        [Test]
        public void ReleaseSlot_ClearsOnlyRequestedFlag()
        {
            var ring = new Ws233ReceiveRing(slotCount: 3, slotSize: 128);
            ring.Backing[ring.FlagsOffset + 0] = 1;
            ring.Backing[ring.FlagsOffset + 1] = 1;
            ring.Backing[ring.FlagsOffset + 2] = 1;

            ring.ReleaseSlot(1);

            Assert.That(ring.Backing[ring.FlagsOffset + 0], Is.EqualTo(1));
            Assert.That(ring.Backing[ring.FlagsOffset + 1], Is.Zero);
            Assert.That(ring.Backing[ring.FlagsOffset + 2], Is.EqualTo(1));
        }

        [Test]
        public void ReleaseAllSlots_ClearsEveryFlag()
        {
            var ring = new Ws233ReceiveRing(slotCount: 3, slotSize: 128);
            ring.Backing[ring.FlagsOffset + 0] = 1;
            ring.Backing[ring.FlagsOffset + 1] = 1;
            ring.Backing[ring.FlagsOffset + 2] = 1;

            ring.ReleaseAllSlots();

            Assert.That(ring.Backing[ring.FlagsOffset + 0], Is.Zero);
            Assert.That(ring.Backing[ring.FlagsOffset + 1], Is.Zero);
            Assert.That(ring.Backing[ring.FlagsOffset + 2], Is.Zero);
        }
    }
}
