using System;
using System.Threading;
using QuickGraph.Algorithms;

namespace GraphSharp.Algorithms
{
    public abstract class AlgorithmBase : IAlgorithm
    {
        private int _cancelling;
        private volatile ComputationState _state = ComputationState.NotRunning;
        private volatile object _syncRoot = new object();

        public Object SyncRoot
        {
            get { return _syncRoot; }
        }

        public ComputationState State
        {
            get
            {
                lock (_syncRoot)
                {
                    return _state;
                }
            }
        }

        public void Compute()
        {
            BeginComputation();
            InternalCompute();
            EndComputation();
        }

        public virtual void Abort()
        {
            bool raise = false;
            lock (_syncRoot)
            {
                if (_state == ComputationState.Running)
                {
                    _state = ComputationState.PendingAbortion;
                    Interlocked.Increment(ref _cancelling);
                    raise = true;
                }
            }
            if (raise)
                OnStateChanged(EventArgs.Empty);
        }

        public event EventHandler StateChanged;

        public event EventHandler Started;

        public event EventHandler Finished;

        public event EventHandler Aborted;

        protected abstract void InternalCompute();

        protected void OnStateChanged(EventArgs e)
        {
            EventHandler eh = StateChanged;
            if (eh != null)
                eh(this, e);
        }

        protected void OnStarted(EventArgs e)
        {
            EventHandler eh = Started;
            if (eh != null)
                eh(this, e);
        }

        protected void OnFinished(EventArgs e)
        {
            EventHandler eh = Finished;
            if (eh != null)
                eh(this, e);
        }

        protected void OnAborted(EventArgs e)
        {
            EventHandler eh = Aborted;
            if (eh != null)
                eh(this, e);
        }

        protected void BeginComputation()
        {
            lock (_syncRoot)
            {
                if (_state != ComputationState.NotRunning)
                    throw new InvalidOperationException();

                _state = ComputationState.Running;
                _cancelling = 0;
                OnStarted(EventArgs.Empty);
                OnStateChanged(EventArgs.Empty);
            }
        }

        protected void EndComputation()
        {
            lock (_syncRoot)
            {
                switch (_state)
                {
                    case ComputationState.Running:
                        _state = ComputationState.Finished;
                        OnFinished(EventArgs.Empty);
                        break;
                    case ComputationState.PendingAbortion:
                        _state = ComputationState.Aborted;
                        OnAborted(EventArgs.Empty);
                        break;
                    default:
                        throw new InvalidOperationException();
                }
                _cancelling = 0;
                OnStateChanged(EventArgs.Empty);
            }
        }
    }
}