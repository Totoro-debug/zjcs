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
public class GameCharacterZSet : MonoBehaviour
{
	public MyTransform Transform;

	[SerializeField]
	private float mOffsetZ;

	private void OnEnable()
	{
		UpdateZ();
		Transform.OnPosUpdated += OnPosUpdated;
	}

	private void OnDisable()
	{
		Transform.OnPosUpdated -= OnPosUpdated;
	}

	private void OnPosUpdated(MyTransform _)
	{
		UpdateZ();
	}

	private void UpdateZ()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		Vector3 position = ((Component)this).transform.position;
		position.z = ZSetUtils.YToZ(position.y) + mOffsetZ;
		((Component)this).transform.position = position;
	}
}
}
