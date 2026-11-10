// Migrated selected original source; see skill movement runtime receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityActions;
namespace Zjcs.Official.BattleSkillPlayables
{
using UnityEngine;

[RequireComponent(typeof(MyAction))]
public class MyActionBase<T> : MyActionBase<T, object>, IEmptyInfoMyComponent where T : IEntityComponentInfoSource
{
}
public class MyActionBase<T, TInfo> : MyComponent<T, TInfo> where T : IEntityComponentInfoSource
{
	private MyAction _Action;

	public MyAction myAction
	{
		get
		{
			if ((Object)(object)_Action == (Object)null)
			{
				_Action = ((Component)this).GetComponent<MyAction>();
			}
			return _Action;
		}
	}

	public MyEntity myApplyEntity
	{
		get
		{
			if ((Object)(object)myAction == (Object)null)
			{
				return null;
			}
			return myAction.myApplyEntity;
		}
	}
}

}
