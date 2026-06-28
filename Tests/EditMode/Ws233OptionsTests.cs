using NUnit.Framework;

namespace Unity233.WebSocket.Tests
{
    public sealed class Ws233OptionsTests
    {
        [Test]
        public void Defaults_AreExpectedForGeneralWebGLUse()
        {
            var options = new Ws233Options();

            Assert.That(options.BufferPool, Is.Null);
            Assert.That(options.ReceiveRingSlotCount, Is.EqualTo(32));
            Assert.That(options.ReceiveRingSlotSize, Is.EqualTo(4096));
        }

        [Test]
        public void WeChatMinigameDefaults_AreTunedForFrequentSmallPackets()
        {
            var options = Ws233Options.WeChatMinigameDefaults;

            Assert.That(options.BufferPool, Is.Null);
            Assert.That(options.ReceiveRingSlotCount, Is.EqualTo(48));
            Assert.That(options.ReceiveRingSlotSize, Is.EqualTo(2048));
        }

        [Test]
        public void WeChatMinigameDefaults_ReturnsFreshInstances()
        {
            var first = Ws233Options.WeChatMinigameDefaults;
            var second = Ws233Options.WeChatMinigameDefaults;

            Assert.That(second, Is.Not.SameAs(first));
        }
    }
}
