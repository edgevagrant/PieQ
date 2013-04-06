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
            var messages = File.Exists(_filePath)
                               ? JsonConvert.DeserializeObject<WorkItem[]>(File.ReadAllText(_filePath),
                                                                           JsonSerializerSettings)
                               : new WorkItem[] {};
            this._queue = new List<WorkItem>(messages);
        }

        private bool working = false;

        public IEnumerable<WorkItem> MessagesSnapshot
        {
            get
            {
                lock (_queue)
                {
                    Func<WorkItem, WorkItem> clone = (m) =>
                        {
                            var json = JsonConvert.SerializeObject(m, Newtonsoft.Json.Formatting.Indented,
                                                                   JsonSerializerSettings);
                            return JsonConvert.DeserializeObject<WorkItem>(json, JsonSerializerSettings);
                        };
                    return _queue.Select(clone).ToArray();
                }
            }
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
            File.WriteAllText(filePath,
                              JsonConvert.SerializeObject(_queue.ToArray(), Newtonsoft.Json.Formatting.Indented,
                                                          JsonSerializerSettings));
        }


        public IDisposable LockScope()
        {
            return new Disposable(() => Monitor.Enter(_queue), () =>
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
                    message.WorkItemState = new FailedState("Was processing when work begun");
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
                Task.Factory.StartNew(() => _jobHost(() => ProcessMessage(next)));
            }

        }

        private void ProcessMessage(WorkItem workItem)
        {
            var sw = new Stopwatch();
            sw.Start();
            try
            {
                workItem.Execute();
                sw.Stop();
                using (LockScope())
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

                using (LockScope())
                {
                    workItem.ExecutionDuration = sw.Elapsed;
                    var innerEx = ex.InnerException != null
                                      ? ("Inner:\r\n" + ex.InnerException.ToString() + "\r\nOuter:\r\n")
                                      : "";
                    workItem.WorkItemState = new FailedState(innerEx + ex.ToString());
                    working = false;
                    SaveQueue(_filePath);
                }
            }
            BeginWork();
        }


        public readonly JobHost _jobHost = a => a();

        private string _filePath = Path.Combine(AppDomain.CurrentDomain.GetData("DataDirectory").ToString(), "workqueue.json");

        public JsonSerializerSettings JsonSerializerSettings { get; set; }


        private static readonly Lazy<WorkQueue> LazyInstance = new Lazy<WorkQueue>(() => new WorkQueue());

        public static WorkQueue Instance
        {
            get { return LazyInstance.Value; }
        }

        public void Clear()
        {
            using (this.LockScope())
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
            using (this.LockScope())
            {
                var runningItem = _queue.SingleOrDefault(i => i.WorkItemState is ProcessingState);
                if (runningItem != null)
                {
                    runningItem.Suspend();
                }
            }
        }
    }
}