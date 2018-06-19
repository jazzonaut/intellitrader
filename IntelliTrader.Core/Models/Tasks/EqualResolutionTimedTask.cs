﻿using System;
using System.Diagnostics;
using System.Threading;

namespace IntelliTrader.Core
{
    public abstract class EqualResolutionTimedTask : ITimedTask
    {
        /// <summary>
        /// Raised on unhandled exception
        /// </summary>
        public event UnhandledExceptionEventHandler UnhandledException;

        /// <summary>
        /// Delay before starting the task in milliseconds
        /// </summary>
        public double StartDelay { get; set; } = 0;

        /// <summary>
        /// Periodic execution interval in milliseconds
        /// </summary>
        public double Interval { get; set; } = 1000;

        /// <summary>
        /// The priority of the timer thread
        /// </summary>
        public ThreadPriority Priorty { get; set; } = ThreadPriority.Normal;

        /// <summary>
        /// Stopwatch to use for timing the intervals
        /// </summary>
        public Stopwatch Stopwatch { get; set; }

        /// <summary>
        /// Indicates whether the task is currently running
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Number of times the task has been run
        /// </summary>
        public long RunCount { get; private set; }

        /// <summary>
        /// Total time it took to run the task in milliseconds
        /// </summary>
        public double TotalRunTime { get; private set; }

        /// <summary>
        /// Total task run delay in milliseconds
        /// </summary>
        public double TotalLagTime { get; private set; }

        private Thread timerThread;
        private Stopwatch runWatch;
        private ManualResetEvent resetEvent;

        /// <summary>
        /// Start the task
        /// </summary>
        public void Start()
        {
            if (!IsRunning)
            {
                IsRunning = true;
                runWatch = new Stopwatch();
                resetEvent = new ManualResetEvent(false);

                timerThread = new Thread(() =>
                {
                    if (Stopwatch == null)
                    {
                        Stopwatch = Stopwatch.StartNew();
                    }
                    else if (!Stopwatch.IsRunning)
                    {
                        Stopwatch.Restart();
                    }

                    long startTime = Stopwatch.ElapsedMilliseconds;
                    while (IsRunning)
                    {
                        long elapsedTime = Stopwatch.ElapsedMilliseconds;
                        double nextRunTime = RunCount * Interval + StartDelay + TotalLagTime + startTime;
                        double waitTime = nextRunTime - elapsedTime;
                        if (waitTime > 0)
                        {
                            if (resetEvent.WaitOne((int)(waitTime)))
                            {
                                break;
                            }
                        }

                        runWatch.Restart();
                        SafeRun();
                        long runTime = runWatch.ElapsedMilliseconds;
                        TotalLagTime += (runTime > Interval) ? (runTime - Interval) : 0;
                        TotalRunTime += runTime;
                        RunCount++;
                    }
                });

                timerThread.Priority = Priorty;
                timerThread.Start();
            }
        }

        /// <summary>
        /// Stop the task
        /// </summary>
        public void Stop()
        {
            Stop(true);
        }

        /// <summary>
        /// Stop the task
        /// </summary>
        /// <remarks>
        /// This function is waiting an executing thread (unless join is set to false).
        /// </remarks>
        public void Stop(bool terminateThread)
        {
            if (IsRunning)
            {
                IsRunning = false;
                resetEvent.Set();
                runWatch.Stop();

                if (!terminateThread)
                {
                    timerThread?.Join();
                    timerThread = null;
                }

                resetEvent.Dispose();
            }
        }

        /// <summary>
        /// Manually run the task
        /// </summary>
        public void RunNow()
        {
            SafeRun();
        }

        /// <summary>
        /// This method must be implemented by the child class and must contain the code
        /// to be executed periodically.
        /// </summary>
        protected abstract void Run();

        /// <summary>
        /// Wrap Run method in Try/Catch
        /// </summary>
        private void SafeRun()
        {
            try
            {
                Run();
            }
            catch (Exception ex)
            {
                UnhandledException?.Invoke(this, new UnhandledExceptionEventArgs(ex, false));
            }
        }
    }
}