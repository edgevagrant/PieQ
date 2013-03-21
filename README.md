PieQ
====

Easy-as-pie embedded persistent async work queue for .NET.

Create classes that inherit from 

```
    public abstract class WorkItem
    {
        ...
        public abstract void Execute();
        ...
    }
```

Then queue them up using:

```
    var myWorkItem = new MyWorkItem();
    WorkQueue.Instance.Queue(myWorkItem);
```

PieQ will queue up the jobs, and run them one at a time. When a task is added, or it's state changes, the 
queue will be persisted to /App_Data automatically. 

To get the queue running without queuing a new item (e.g. on application start):
```
WorkQueue.Instance.ResumeProcessing(); 
```

To shut the queue down gracefully
```
WorkQueue.Instance.CeaseProcessing(); //this will block until the executing task has finished. 
```


If you want your tasks to handle resume/cease gracefully, you can override the following
```
    public abstract class WorkItem
    {
        ...
        protected void Suspend(){}; //will be called when trying to shutdown gracefully. This should try to stop the job.
        protected void Resume(){}; //will be called when restarting after a non-graceful shutdown 
        protected void Recover(){}; //will be called when restarting after a non-graceful shutdown (e.g. a crash)
        ...
    }
```
