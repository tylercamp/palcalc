using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver
{
    class LocalListPool<T>(int initialCapacity)
    {
        private readonly Stack<List<T>> pool = new(initialCapacity);

        public List<T> Borrow()
        {
            if (pool.Count == 0) return new List<T>(capacity: 8);
            else return pool.Pop();
        }

        public List<T> BorrowWith(IEnumerable<T> initialValues)
        {
            var res = Borrow();
            res.AddRange(initialValues);
            return res;
        }

        public void Return(List<T> value)
        {
            value.Clear();
            pool.Push(value);
        }
    }

    class LocalObjectPool<T>(int initialCapacity) where T : new()
    {
        private readonly Stack<T> pool = new(initialCapacity);

        public T Borrow()
        {
            if (pool.Count == 0) return new T();
            else return pool.Pop();
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
