using System.Collections.Generic;

namespace GraphSharp.Helpers
{
	public delegate void DisposingHandler( object sender );

	public interface IPoolObject
	{
		void Reset();
		void Terminate();
		event DisposingHandler Disposing;
	}

	public class ObjectPool<T>
		where T : class, IPoolObject, new()
	{
		private const int PoolSize = 1024;

		private readonly Queue<T> _pool = new Queue<T>();

		private readonly bool _allowPoolGrowth = true;
		private readonly int _initialPoolSize;
		private int _activePoolObjectCount;

		/// <summary>Pool constructor, pool will allow growth.</summary>
		public ObjectPool()
			: this( PoolSize, true )
		{
		}

		/// <summary>Pool constructor.</summary>
		/// <param name="initialPoolSize">Initial pool size.</param>
		/// <param name="allowPoolGrowth">Allow pool growth or not.</param>
		public ObjectPool(int initialPoolSize, bool allowPoolGrowth)
		{
			_initialPoolSize = initialPoolSize;
			_allowPoolGrowth = allowPoolGrowth;

			InitializePool();
		}

		/// <summary>Fills the pool with objects.</summary>
		private void InitializePool()
		{
			//adds some objects to the pool
			for ( int i = 0; i < _initialPoolSize; i++ )
				CreateObject();
		}

		/// <summary>This method will create a new pool object if the pool is not full or allow growth and adds it to the pool.</summary>
		/// <returns>Returns with the newly created object or default(T) if the pool is full.</returns>
		private T CreateObject()
		{
			if ( _activePoolObjectCount >= _initialPoolSize && !_allowPoolGrowth )
				return null;

			var newObject = new T();
			newObject.Disposing += ObjectDisposing;

			Add( newObject );

			return newObject;
		}

		/// <summary>This method adds the object to the pool and increases the actual pool size.</summary>
		/// <param name="poolObject">The object which should be added to the pool.</param>
		private void Add( T poolObject )
		{
			_pool.Enqueue( poolObject );
			_activePoolObjectCount += 1;
		}

        /// <summary>It puts back the disposed poolObject into the pull.</summary>
        /// <param name="sender">The disposed pool object.</param>
		private void ObjectDisposing( object sender )
		{
			lock ( this )
			{
				var poolObject = sender as T;
			    if (poolObject == null)
			        return;

				_activePoolObjectCount -= 1;
			    if (_pool.Count < _initialPoolSize)
				{
					poolObject.Reset();
					Add( poolObject );
				}
				else
					poolObject.Terminate();
			}
		}

		/// <summary>Gets an object from the pool.</summary>
		/// <returns>Returns with the object or null if there isn't any 
		/// free objects and the pool does not allow growth.</returns>
		public T GetObject()
		{
			lock ( this )
			{
				if ( _pool.Count == 0 )
				{
					if ( !_allowPoolGrowth )
						return null;

					T newObject = CreateObject();
					_pool.Clear();
					return newObject;
				}
				
				return _pool.Dequeue();
			}
		}
	}
}
