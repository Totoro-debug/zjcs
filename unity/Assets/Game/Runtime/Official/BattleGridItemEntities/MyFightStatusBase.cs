// Original client component; see grid entity runtime source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityClientComponents;
using Zjcs.Official.StoryAnimation;
using Zjcs.Official.StorySourceFade;
using Zjcs.Official.BattleStatusPlayables;
using UnityEngine;


namespace Zjcs.Official.BattleGridItemEntities
{
[RequireComponent(typeof(MyFightStatus))]
public class MyFightStatusBase<T> : MyFightStatusBase<T, object>, IEmptyInfoMyComponent
{
}
[RequireComponent(typeof(MyFightStatus))]
public class MyFightStatusBase<T, TInfo> : MyComponent<T, TInfo>
{
	private MyFightStatus _myStatus;

	public MyFightStatus myStatus
	{
		get
		{
			if ((Object)(object)_myStatus == (Object)null)
			{
				_myStatus = ((Component)this).GetComponent<MyFightStatus>();
			}
			return _myStatus;
		}
	}
}
}
