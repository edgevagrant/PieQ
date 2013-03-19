using System;

namespace PieQ
{
    [Serializable]
    public abstract class Message
    {
        public DateTimeOffset ReceivedAt { get; internal set; }
        public MessageState MessageState {  get; internal set; }
        public TimeSpan? ExecutionDuration { get; internal set; }
        public abstract void Execute();
    }
}