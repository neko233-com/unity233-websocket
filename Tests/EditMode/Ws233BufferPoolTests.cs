using NUnit.Framework;

namespace Unity233.WebSocket.Tests
{
    public sealed class Ws233BufferPoolTests
    {
        [TestCase(0, 256)]
        [TestCase(1, 256)]
        [TestCase(255, 256)]
        [TestCase(256, 256)]
        [TestCase(257, 512)]
        [TestCase(4096, 4096)]
        [TestCase(4097, 8192)]
        public void Rent_RoundsToExpectedPowerOfTwoBucket(int minimumLength, int expectedLength)
        {
            var pool = new Ws233BufferPool();

            var buffer = pool.Rent(minimumLength);

            Assert.That(buffer, Is.Not.Null);
            Assert.That(buffer.Length, Is.EqualTo(expectedLength));
        }

        [Test]
        public void Return_ReusesPowerOfTwoBuffer()
        {
            var pool = new Ws233BufferPool();
            var first = pool.Rent(300);

            pool.Return(first);
            var second = pool.Rent(300);

            Assert.That(second, Is.SameAs(first));
        }

        [Test]
        public void Return_IgnoresNullEmptyAndNonBucketBuffers()
        {
            var pool = new Ws233BufferPool();

            Assert.DoesNotThrow(() => pool.Return(null));
            Assert.DoesNotThrow(() => pool.Return(new byte[0]));
            Assert.DoesNotThrow(() => pool.Return(new byte[300]));

            var rented = pool.Rent(257);
            Assert.That(rented.Length, Is.EqualTo(512));
        }

        [Test]
        public void Return_RespectsMaxPerBucket()
        {
            var pool = new Ws233BufferPool(maxPerBucket: 1);
            var first = pool.Rent(128);
            var second = pool.Rent(128);

            pool.Return(first);
            pool.Return(second);

            var reused = pool.Rent(128);
            var fresh = pool.Rent(128);

            Assert.That(ReferenceEquals(reused, first) || ReferenceEquals(reused, second), Is.True);
            Assert.That(fresh, Is.Not.SameAs(first));
            Assert.That(fresh, Is.Not.SameAs(second));
        }
    }
}
