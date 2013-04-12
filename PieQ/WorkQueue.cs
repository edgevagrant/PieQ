using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace PieQ
{
    public class WorkQueue
    {
        public WorkQueue()
        {
            JsonSerializerSettings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Objects,
                    ContractResolver = new PrivateSetterDefaultContractResolver(),
                };
            WorkItem[] messages;
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                messages = JsonConvert.DeserializeObject<WorkItem[]>(json, JsonSerializerSettings);
                QueueSnapshot = JsonConvert.DeserializeObject<WorkItem[]>(json, JsonSerializerSettings);
            }
            else
            {
                messages = new WorkItem[] {};
                QueueSnapshot = new WorkItem[]{};
            }
            this._queue = new List<WorkItem>(messages);
        }

        private bool working = false;

        public IEnumerable<WorkItem> QueueSnapshot { get; private set; }

        private WorkItem CloneQueue(WorkItem m)
        {
            var json = JsonConvert.SerializeObject(m, Newtonsoft.Json.Formatting.Indented, JsonSerializerSettings);
            return JsonConvert.DeserializeObject<WorkItem>(json, JsonSerializerSettings);
        }

        private readonly List<WorkItem> _queue = new List<WorkItem>();


        public void Queue(WorkItem workItem)
        {
            lock (_queue)
            {
                workItem.WorkItemState = new QueuedState();
                workItem.ReceivedAt = DateTime.UtcNow;
                _queue.Add(workItem);
                SaveQueue(_filePath);
                if (!working) BeginWork();
            }
        }

        private void SaveQueue(string filePath)
        {
            var json = JsonConvert.SerializeObject(_queue.ToArray(), Formatting.Indented, JsonSerializerSettings);
            var hashCode = json.GetHashCode();
            if (hashCode != jsonhash)
            {
                jsonhash = hashCode;
                File.WriteAllText(filePath, json);
                QueueSnapshot = JsonConvert.DeserializeObject<WorkItem[]>(json, JsonSerializerSettings);
            }
        }


        public WorkQueueSession OpenSession()
        {
            return new WorkQueueSession(this, () => Monitor.Enter(_queue), () =>
                {
                    SaveQueue(_filePath);
                    Monitor.Exit(_queue);
                });
        }

        public void BeginWork()
        {
            WorkItem next = null;
            lock (_queue)
            {
                var processing = _queue.Where(m => m.WorkItemState is ProcessingState);
                foreach (var message in processing)
                {
                    message.WorkItemState = new FailedState("Was processing when work begun", "");
                }
                if (processing.Any())
                {
                    SaveQueue(_filePath);
                }

                next = _queue.FirstOrDefault(m => m.WorkItemState is QueuedState);

                if (next != null)
                {
                    working = true;
                    next.WorkItemState = new ProcessingState();
                    SaveQueue(_filePath);
                }
            }
            if (next != null)
            {
                Task.Factory.StartNew(() => _jobHost(() => ProcessWorkItem(next)));
            }

        }

        private void ProcessWorkItem(WorkItem workItem)
        {
            var sw = new Stopwatch();
            sw.Start();
            try
            {
                workItem.Execute();
                sw.Stop();
                using (OpenSession())
                {
                    workItem.ExecutionDuration = sw.Elapsed;
                    workItem.WorkItemState = new SucceededState();
                    working = false;
                    SaveQueue(_filePath);
                }

            }
            catch (Exception ex)
            {
                sw.Stop();

                using (OpenSession())
                {
                    workItem.ExecutionDuration = sw.Elapsed;
                    var innerEx = ex.InnerException != null
                                      ? ("Inner:\r\n" + ex.InnerException.ToString() + "\r\nOuter:\r\n")
                                      : "";
                    workItem.WorkItemState = new FailedState(ex.Message, ex.StackTrace);
                    working = false;
                    SaveQueue(_filePath);
                }
            }
            BeginWork();
        }


        public readonly JobHost _jobHost = a => a();

        private string _filePath = Path.Combine((AppDomain.CurrentDomain.GetData("DataDirectory") ?? Directory.GetCurrentDirectory()).ToString(), "workqueue.json");

        public JsonSerializerSettings JsonSerializerSettings { get; set; }


        private static readonly Lazy<WorkQueue> LazyInstance = new Lazy<WorkQueue>(() => new WorkQueue());
        private int jsonhash;

        public static WorkQueue Instance
        {
            get { return LazyInstance.Value; }
        }

        public void Clear()
        {
            using (this.OpenSession())
            {
                if (working)
                {
                    throw new Exception("Cannot clear while working");
                }
                _queue.Clear();
            }
        }

        public void CeaseProcessing()
        {
            using (this.OpenSession())
            {
                var runningItem = _queue.SingleOrDefault(i => i.WorkItemState is ProcessingState);
                if (runningItem != null)
                {
                    runningItem.Suspend();
                }
            }
        }
        public class WorkQueueSession : Disposable
        {
            private readonly WorkQueue _queue;

            public WorkQueueSession(WorkQueue queue, Action begin, Action dispose)
                : base(begin, dispose)
            {
                _queue = queue;
            }
            public IList<WorkItem> WorkItems { get { return _queue._queue; }}
        }
    }

    
}