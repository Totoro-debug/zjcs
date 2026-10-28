// Complete original client lifecycle class; see entity lifecycle source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13EntityEvents;
using IEventReceiver = Zjcs.Official.World.Map13EntityEvents.IEventReceiver;
using UnityEngine;

namespace Zjcs.Official.EntityLifecycle
{
public class MyComponent : MonoBehaviour
{
	protected MyEntity _myEntity;

	private ILogger mLogger;

	public MyEntity myEntity
	{
		get
		{
			if ((Object)(object)_myEntity == (Object)null)
			{
				_myEntity = ((Component)this).GetComponent<MyEntity>();
			}
			return _myEntity;
		}
	}

	public MyContainer myContainer => myEntity.myContainer;

	public MyTransform myTransform => myEntity.myTransform;

	public MyGridTransform myGridTransform => myEntity.myGridTransform;

	public ILogger Logger => mLogger ?? (mLogger = (((Object)(object)myEntity != (Object)null) ? myEntity.Logger : MyContainer.CreateLogger("MyComponentWithoutEntity_" + ((Object)this).name + "_" + ((object)this).GetType().FullName)));

	public virtual void Bind()
	{
	}

	public virtual void OnStart(bool newEntity)
	{
	}

	public virtual object GetEcComponent()
	{
		return null;
	}

	public virtual void OnEnd(object userData)
	{
	}

	public virtual void OnRemove(object userData)
	{
	}

	public virtual void OnImmediateToEnd()
	{
	}
}
public class MyComponent<T> : MyComponent<T, object>, IEmptyInfoMyComponent
{
}
public class MyComponent<T, TInfo> : MyComponent
{
	public T Component;

	public TInfo Info => (TInfo)EntityLifecycleServices.Info(Component);

	public override object GetEcComponent()
	{
		return Component;
	}

	protected virtual void Awake()
	{
	}

	public override void Bind()
	{
		Component = myEntity.Entity.GetComponent<T>();
	}

	protected virtual void OnDestroy()
	{
	}
}

}
