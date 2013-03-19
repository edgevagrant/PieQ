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
            var messages = File.Exists(_filePath) ? JsonConvert.DeserializeObject<Message[]>(File.ReadAllText(_filePath), JsonSerializerSettings) : new Message[] { };
            this._queue = new List<Message>(messages);
        }
        private bool working = false;

        public IEnumerable<Message> MessagesSnapshot
        {
            get
            {
                lock (_queue)
                {
                    Func<Message, Message> clone = (m)=>
                    {
                        var json = JsonConvert.SerializeObject(m, Newtonsoft.Json.Formatting.Indented, JsonSerializerSettings);
                        return JsonConvert.DeserializeObject<Message>(json, JsonSerializerSettings);
                    };
                    return _queue.Select(clone).ToArray();
                }
            }
        }

        readonly List<Message> _queue = new List<Message>();

        
        public void AddMessage(Message message)
        {
            lock (_queue)
            {
                message.MessageState = new QueuedState();
                message.ReceivedAt = DateTime.UtcNow;
                _queue.Add(message);
                SaveQueue(_filePath);
                if (!working) BeginWork();
            }
        }

        private void SaveQueue(string filePath)
        {
            File.WriteAllText(filePath, JsonConvert.SerializeObject(_queue.ToArray(), Newtonsoft.Json.Formatting.Indented, JsonSerializerSettings));
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
            Message next = null;
            lock (_queue)
            {
                var processing = _queue.Where(m => m.MessageState is ProcessingState);
                foreach (var message in processing)
                {
                    message.MessageState = new FailedState("Was processing when work begun");
                }
                if (processing.Any())
                {
                    SaveQueue(_filePath);
                }

                next = _queue.FirstOrDefault(m => m.MessageState is QueuedState);

                if (next != null)
                {
                    working = true;
                    next.MessageState = new ProcessingState();
                    SaveQueue(_filePath);
                }
            }
            if (next != null)
            {
                Task.Factory.StartNew(() => _jobHost(() => ProcessMessage(next)));
            }

        }

        private void ProcessMessage(Message message)
        {
            var sw = new Stopwatch();
            sw.Start();
            try
            {
                message.Execute();
                sw.Stop();
                using (LockScope())
                {
                    message.ExecutionDuration = sw.Elapsed;
                    message.MessageState = new SucceededState();
                    working = false;
                    SaveQueue(_filePath);
                }

            }
            catch (Exception ex)
            {
                sw.Stop();

                using (LockScope())
                {
                    message.ExecutionDuration = sw.Elapsed;
                    var innerEx = ex.InnerException != null ? ("Inner:\r\n" + ex.InnerException.ToString() + "\r\nOuter:\r\n") : "";
                    message.MessageState = new FailedState(innerEx + ex.ToString());
                    working = false;
                    SaveQueue(_filePath);
                }
            }
            BeginWork();
        }


        public  readonly JobHost _jobHost = a => a();
        private string _filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "workqueue.json");
        public JsonSerializerSettings JsonSerializerSettings { get; set; }


        private static readonly Lazy<WorkQueue> LazyInstance = new Lazy<WorkQueue>(() => new WorkQueue());
        public static WorkQueue Instance { get { return LazyInstance.Value; } }

        public void Clear()
        {
            using(this.LockScope())
            {
                if (working)
                {
                    throw new Exception("Cannot clear while working");
                }
                _queue.Clear();
            }
        }
    }
}