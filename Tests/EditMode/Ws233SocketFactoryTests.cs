using NUnit.Framework;

namespace Unity233.WebSocket.Tests
{
    public sealed class Ws233SocketFactoryTests
    {
        [Test]
        public void Create_UsesProvidedPoolAndAddress()
        {
            var pool = new Ws233BufferPool();
            using var socket = Ws233Socket.Create("wss://example.invalid/ws", pool);

            Assert.That(socket.Address, Is.EqualTo("wss://example.invalid/ws"));
            Assert.That(socket.BufferPool, Is.SameAs(pool));
            Assert.That(socket.ReadyState, Is.EqualTo(Ws233State.Closed));
        }

        [Test]
        public void Create_WithNullOptionsStillCreatesSocket()
        {
            using var socket = Ws233Socket.Create("wss://example.invalid/ws", (Ws233Options)null);

            Assert.That(socket.Address, Is.EqualTo("wss://example.invalid/ws"));
            Assert.That(socket.BufferPool, Is.Not.Null);
        }

        [Test]
        public void Send_WhenSocketIsClosedRaisesErrorInsteadOfThrowing()
        {
            using var socket = Ws233Socket.Create("wss://example.invalid/ws", new Ws233BufferPool());
            string error = null;
            socket.Errored += message => error = message;

            Assert.DoesNotThrow(() => socket.Send(new byte[] { 1, 2, 3 }));

            Assert.That(error, Is.EqualTo("WebSocket is not open."));
        }

        [Test]
        public void SendText_WhenSocketIsClosedRaisesErrorInsteadOfThrowing()
        {
            using var socket = Ws233Socket.Create("wss://example.invalid/ws", new Ws233BufferPool());
            string error = null;
            socket.Errored += message => error = message;

            Assert.DoesNotThrow(() => socket.SendText("hello"));

            Assert.That(error, Is.EqualTo("WebSocket is not open."));
        }
    }
}
