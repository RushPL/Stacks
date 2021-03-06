﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Stacks
{
#if MONO
    public class ActionBlockExecutor : SynchronizationContext, IExecutor
    {
        private BlockingCollection<Action> col;
        private TaskCompletionSource<int> tcs;
        private volatile bool isStopping;
        private Thread runner;

        private readonly string name;
        private readonly bool supportSynchronizationContext;

        public event Action<Exception> Error;
          
        public ActionBlockExecutor()
            : this(null, new ActionBlockExecutorSettings())
        { }

        public ActionBlockExecutor(string name)
            : this(name, new ActionBlockExecutorSettings())
        { }

        public ActionBlockExecutor(string name, ActionBlockExecutorSettings settings)
        {
            this.name = name;
            this.supportSynchronizationContext = settings.SupportSynchronizationContext;
         
            if (settings.QueueBoundedCapacity <= 0)
                col = new BlockingCollection<Action>();
            else
                col = new BlockingCollection<Action>(settings.QueueBoundedCapacity);
           
            tcs = new TaskCompletionSource<int>();
            runner = new Thread(new ThreadStart(Run));
            runner.IsBackground = true;
            runner.Start();
        }

        private void Run()
        {
            Action a;

            while (true)
            {
                if (this.isStopping && col.Count == 0)
                    break;

                if (col.TryTake(out a, 50))
                {
                    ExecuteAction(a);
                }
            }
            
            tcs.SetResult(0);
        }

        private void ExecuteAction(Action a)
        {
            SynchronizationContext oldCtx = null;

            if (supportSynchronizationContext)
            {
                oldCtx = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(this);
            }

            try
            {
                a();
            }
            catch (Exception e)
            {
                ErrorOccured(e);
            }
            finally
            {
                if (supportSynchronizationContext)
                {
                    SynchronizationContext.SetSynchronizationContext(oldCtx);
                }
            }
        }

        private void ErrorOccured(Exception e)
        {
            OnError(e);
            isStopping = true;
        }

        private void OnError(Exception e)
        {
            var h = Error;
            if (h != null)
            {
                try { h(e); }
                catch { }
            }
        }

        public void Enqueue(Action action)
        {
            if (!isStopping)
                col.Add(action);
        }

        public Task Stop()
        {
            isStopping = true;
            return tcs.Task as Task;
        }

        public Task Completion
        {
            get { return tcs.Task as Task; }
        }

        public SynchronizationContext Context
        {
            get { return this; }
        }

        public override string ToString()
        {
            return "ActionBlock Executor " +
                (name == null ? "" : string.Format("({0})", name));
        }
    }
#endif
}
