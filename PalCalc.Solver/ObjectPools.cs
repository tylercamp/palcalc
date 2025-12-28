using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver
{
    public interface IObjectPool<T>
    {
        PooledReference<T> Borrow();
        T BorrowRaw();

        void Return(T value);
    }

    public struct PooledReference<T> : IDisposable
    {
        private readonly IObjectPool<T> pool;
        private bool retained;

        public T Value { get; }

        internal PooledReference(T value, IObjectPool<T> owner)
        {
            Value = value;
            this.pool = owner;
            this.retained = false;
        }

        /// <summary>
        /// Prevents the value from being returned to the pool when this reference is disposed.
        /// Call this when you want to keep the value beyond the scope of the using block.
        /// </summary>
        public void Retain() => retained = true;

        public void Dispose()
        {
            if (!retained)
                pool.Return(Value);
        }
    }

    class LocalListPool<T>(int initialCapacity) : IObjectPool<List<T>>
    {
        private Stack<List<T>> pool = new(initialCapacity);

        public PooledReference<List<T>> Borrow()
        {
            var value = pool.Count == 0 ? new List<T>(capacity: 8) : pool.Pop();
            return new PooledReference<List<T>>(value, this);
        }

        public PooledReference<List<T>> BorrowWith(IEnumerable<T> initialValues)
        {
            var value = pool.Count == 0 ? new List<T>(capacity: 8) : pool.Pop();
            value.AddRange(initialValues);
            return new PooledReference<List<T>>(value, this);
        }

        public List<T> BorrowRaw()
        {
            return pool.Count == 0 ? new List<T>(capacity: 8) : pool.Pop();
        }

        public List<T> BorrowRawWith(IEnumerable<T> initialValues)
        {
            var value = BorrowRaw();
            value.AddRange(initialValues);
            return value;
        }

        public void Return(List<T> value)
        {
            value.Clear();
            pool.Push(value);
        }
    }

    class LocalObjectPool<T>(int initialCapacity) : IObjectPool<T> where T : new()
    {
        private Stack<T> pool = new(initialCapacity);

        public PooledReference<T> Borrow()
        {
            var value = pool.Count == 0 ? new T() : pool.Pop();
            return new PooledReference<T>(value, this);
        }

        /// <summary>
        /// Borrows an object without automatic return management. Use for cases where the object
        /// needs to be returned from a method or stored in a collection. Caller must manually Return().
        /// </summary>
        public T BorrowRaw()
        {
            return pool.Count == 0 ? new T() : pool.Pop();
        }

        public void Return(T value)
        {
            pool.Push(value);
        }
    }

    class ObjectPoolFactory
    {
        Dictionary<Type, object> pools = [];

        public LocalListPool<T> GetListPool<T>()
        {
            var type = typeof(LocalListPool<T>);
            if (pools.TryGetValue(type, out var existingPool)) return (LocalListPool<T>)existingPool;

            var res = new LocalListPool<T>(16);
            pools.Add(type, res);
            return res;
        }

        public LocalObjectPool<T> GetObjectPool<T>() where T : new()
        {
            var type = typeof(LocalObjectPool<T>);
            if (pools.TryGetValue(type, out var existingPool)) return (LocalObjectPool<T>)existingPool;

            var res = new LocalObjectPool<T>(16);
            pools.Add(type, res);
            return res;
        }
    }
}
