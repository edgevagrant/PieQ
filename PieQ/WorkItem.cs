using System;

namespace PieQ
{
    [Serializable]
    public abstract class WorkItem
    {
        public DateTimeOffset ReceivedAt { get; internal set; }
        public WorkItemState WorkItemState {  get; internal set; }
        public TimeSpan? ExecutionDuration { get; internal set; }
        public abstract void Execute();
    }
}