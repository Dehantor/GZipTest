using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace GZipTest
{
    /// <summary>
    /// Реализация примитивного пула потоков 
    /// </summary>
    class PrimitiveThreadPool : IDisposable
    {
        List<Thread> _threadsFree;
        List<Action> _actions;
        bool _disposed;
        public PrimitiveThreadPool()
        {
            _threadsFree = new List<Thread>();
            _actions = new List<Action>();
            //потоки запускаются и ожидают поступление задач
            for (var i = 0; i < Environment.ProcessorCount; ++i)
            {
                var worker = new Thread(ThreadLoop);
                worker.Start();
                _threadsFree.Add(worker);
            }
        }
        ~PrimitiveThreadPool()
        {
            if(!_disposed)
            {
                Dispose();
            }
        }

        public void Dispose()
        {
            lock (_actions)
            {
                if (!_disposed)
                {

                    while (_actions.Count > 0)
                    {
                        Monitor.Wait(_actions);
                    }
                    Monitor.PulseAll(_actions);
                }
            }
            _disposed = true;
            foreach (var _thread in _threadsFree)
                _thread.Join();
        }

       
        /// <summary>
        /// Добавление задания в очередь
        /// </summary>
        /// <param name="task"></param>
        public void AddTask(Action task)
        {
            lock (_actions)
            {
                _actions.Add(task);
                Monitor.PulseAll(_actions);
            }
        }
        /// <summary>
        /// выполнение потоком задачи
        /// </summary>
        private void ThreadLoop()
        {
            while (true)
            {
                //задача на выполнение
                Action task;
                lock (_actions)
                {
                    while (true)
                    {

                        if (_disposed)
                            return;
                        //берем задачу на выполнение
                        if (_threadsFree.Count > 0)
                            if (_actions.Count > 0 && ReferenceEquals(Thread.CurrentThread, _threadsFree[0]))
                            {
                                task = _actions[0];
                                _actions.RemoveAt(0);
                                _threadsFree.RemoveAt(0);
                                Monitor.PulseAll(_actions);
                                break;
                            }
                        Monitor.Wait(_actions);
                    }
                }
                //выполняем
                task();
                //возвращаем поток к свободным
                lock (_actions)
                {
                    _threadsFree.Add(Thread.CurrentThread);
                }
            }
        }


    }
}
