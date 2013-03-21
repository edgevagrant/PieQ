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
            var message1 = new WaitForSignalWorkItem();
            var message2 = new WaitForSignalWorkItem();
            instance.AddMessage(message1);
            instance.AddMessage(message2);
            Assert.IsInstanceOf<ProcessingState>(message1.WorkItemState);
            Assert.IsInstanceOf<QueuedState>(message2.WorkItemState);
            message1.Waiter.Set();
            Thread.Sleep(1000);
            Assert.IsInstanceOf<SucceededState>(message1.WorkItemState);
            Assert.IsInstanceOf<ProcessingState>(message2.WorkItemState);
            message2.Waiter.Set();
            Thread.Sleep(1000);
            Assert.IsInstanceOf<SucceededState>(message2.WorkItemState);
            Assert.True(instance.MessagesSnapshot.All(m => m.WorkItemState is SucceededState));
            Assert.True(instance.MessagesSnapshot.All(m => m is WaitForSignalWorkItem));

        }
    }
}