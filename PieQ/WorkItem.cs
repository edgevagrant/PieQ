using System;
using System.Threading;

namespace PieQ
{
    public abstract class WorkItem
    {
        public WorkItem()
        {
            WorkItemId = Guid.NewGuid().ToString();
        }
        public virtual string Type { get { return this.GetType().Name.Replace("WorkItem", ""); } }

        public virtual DateTimeOffset ReceivedAt {  get; set; }
        public virtual WorkItemState WorkItemState { get; set; }

        public virtual TimeSpan? ExecutionDuration { get; set; }
        public virtual string WorkItemId { get; set; }

        public abstract void Execute();

        protected void Resume()
        {
        }

        protected void Recover()
        {
            if (!(this.WorkItemState is ProcessingState))
            {
                throw new Exception("Recovery can only occur when a task is not running and is Processing");
            }
            this.WorkItemState = new FailedState("", "");
        }

        protected internal void Suspend()
        {
        }
    }
}