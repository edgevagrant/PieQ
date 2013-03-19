using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace PieQ.Tests
{
    public class QueueTests
    {
        [Test]
        public void CanRunThroughAQueue()
        {
            var instance = new WorkQueue();
            instance.Clear();
            var message1 = new WaitForSignalMessage();
            var message2 = new WaitForSignalMessage();
            instance.AddMessage(message1);
            instance.AddMessage(message2);
            Assert.IsInstanceOf<ProcessingState>(message1.MessageState);
            Assert.IsInstanceOf<QueuedState>(message2.MessageState);
            message1.Waiter.Set();
            Thread.Sleep(1000);
            Assert.IsInstanceOf<SucceededState>(message1.MessageState);
            Assert.IsInstanceOf<ProcessingState>(message2.MessageState);
            message2.Waiter.Set();
            Thread.Sleep(1000);
            Assert.IsInstanceOf<SucceededState>(message2.MessageState);
            Assert.True(instance.MessagesSnapshot.All(m => m.MessageState is SucceededState));
            Assert.True(instance.MessagesSnapshot.All(m => m is WaitForSignalMessage));

        }
    }
}