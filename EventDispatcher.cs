using System;
using System.Collections.Generic;
using Cratesmith.Collections.Temp;

public class EventDispatcher
{
    /// <summary>
    /// The global dispatcher. 
    /// It's not advised to send events to it however, as most things have local dispatchers
    /// </summary>
    public static readonly EventDispatcher Global = new EventDispatcher();

    public void Add(EventDispatcher otherDispatcher)
    {
        if(otherDispatcher==null) throw new ArgumentNullException();
        m_otherDispatchers.Add(otherDispatcher);
    }

    public void Remove(EventDispatcher otherDispatcher)
    {
        m_otherDispatchers.Remove(otherDispatcher);
    }

	public void Add<T>(Action<T> listener)
	{
		var table = Get<T>();
		table.dispatch += listener;
	}

	public void Remove<T>(Action<T> listener)
	{
		var table = Get<T>();
		table.dispatch -= listener;
	}

	public void Clear()
	{
		m_dispatchers.Clear();
	}

    /// <summary>
	/// Creates a disposable dispatch wrapper. 
	/// </summary>
	/// <typeparam name="T">Type of the message payload</typeparam>
	/// <returns></returns>
	public void Send<T>() where T:IDisposable,new()
	{
		var x = TempEventWrapper<T>.Get(this);
		x.Dispose();	
	}


	/// <summary>
	/// Creates a disposable dispatch wrapper. 
	/// </summary>
	/// <typeparam name="T">Type of the message payload</typeparam>
	/// <returns></returns>
	public TempEventWrapper<T> SendScope<T>() where T:IDisposable,new()
	{
		return TempEventWrapper<T>.Get(this);
	}

	public void SendRaw<T>(T message)
    {
        using (var tempHash = TempHashSet<EventDispatcher>.Get())
        {
            SendInternal(message, tempHash.hashSet);
        }
    }

    private void SendInternal(object message, HashSet<EventDispatcher> sentToSet)
    {
        if (!sentToSet.Add(this) || message==null)
        {
            return;
        }

        // call all cached dispatchers (copy before execute in case any register listeners)
        using (var callList = TempList<IBase>.Get())
        {
            callList.list.AddRange(GetOrCreateCache(message.GetType()));
            foreach (var table in callList)
            {
                table.Dispatch(message);
            }
        }

        m_otherDispatchers.RemoveWhere(x => x == null);
        foreach (var mOtherDispatcher in m_otherDispatchers)
        {
            mOtherDispatcher.SendInternal(message, sentToSet);
        }

        if (this != Global)
        {
            Global.SendInternal(message,sentToSet);
        }
    }

    private HashSet<IBase> GetOrCreateCache(Type messageType)
	{
		HashSet<IBase> dispatcherLookup;
		if (!m_cacheDispatchForType.TryGetValue(messageType, out dispatcherLookup))
		{
			dispatcherLookup = m_cacheDispatchForType[messageType] = new HashSet<IBase>();

			using (var list = TempList<KeyValuePair<Type, IBase>>.Get())
			{
				list.list.AddRange(m_dispatchers);
				foreach (var element in list)
				{
					if (!CheckAssignableFrom(element.Key, messageType))
					{
						continue;
					}

					// cache dispatcher
					dispatcherLookup.Add(element.Value);
				}
			}
		}

		return dispatcherLookup;
	}

	#region internal
	interface IBase
	{
		void Dispatch(object message);
	}

	class Table<T> : IBase
	{
		public Action<T> dispatch;

		public void Dispatch(object message)
		{
		    if (message is IConditionalEvent<T> condition && !condition.EventConditionMet)
		    {
		        return;
		    }

			if(dispatch!=null)
			{
				dispatch((T)message);
			}	
		}
	}

	private static Dictionary<Type, Dictionary<Type, bool>> s_typeAssignableFromCache = new Dictionary<Type, Dictionary<Type, bool>>();
	private static bool CheckAssignableFrom(Type typeA, Type typeB)
	{
		Dictionary<Type, bool> typeSet;
		if(!s_typeAssignableFromCache.TryGetValue(typeA, out typeSet))
		{
			s_typeAssignableFromCache[typeA] = typeSet = new Dictionary<Type, bool>();
		}

		bool result = false;
		if(!typeSet.TryGetValue(typeB, out result))
		{
			result = typeSet[typeB] = typeA.IsAssignableFrom(typeB);
		}

		return result;
	}

    private HashSet<EventDispatcher>            m_otherDispatchers = new HashSet<EventDispatcher>();
	private Dictionary<Type, IBase>             m_dispatchers = new Dictionary<Type, IBase>();
	private Dictionary<Type, HashSet<IBase>>    m_cacheDispatchForType = new Dictionary<Type, HashSet<IBase>>();

	Table<T> Get<T>()
	{
		IBase table;
		if(!m_dispatchers.TryGetValue(typeof(T), out table))
		{
			table = m_dispatchers[typeof(T)] = new Table<T>();

			// add this new table to any existing caches
			foreach (var element in m_cacheDispatchForType)
			{
				if (!CheckAssignableFrom(typeof(T), element.Key))
				{
					continue;
				}
                
				element.Value.Add(table);
			}
		}

		return (Table<T>)table; 
	}
	#endregion

	public class TempEventWrapper<T> : IDisposable where T:IDisposable,new()
	{
		private static readonly Queue<TempEventWrapper<T>> s_lists = new Queue<TempEventWrapper<T>>();
		public T value = new T();
		private EventDispatcher dispatcher;

		// acquire a temporary instance
		public static TempEventWrapper<T> Get(EventDispatcher dispatcher)
		{
			var result = s_lists.Count > 0
				? s_lists.Dequeue()
				: new TempEventWrapper<T>();

			result.dispatcher = dispatcher;
			return result;
		}

		public static implicit operator T(TempEventWrapper<T> from)
		{
			return from != null ? from.value : default(T);
		}

		// send then return to pool
		public void Dispose()
		{
			if (dispatcher != null)
			{
				dispatcher.SendRaw(value);
			}

			value.Dispose();					
			s_lists.Enqueue(this);
		}
	}
}

public interface IConditionalEvent<TSelfType> 
{
    bool EventConditionMet { get; }
}
