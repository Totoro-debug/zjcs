// Official client 884_156692; original request/handler control flow.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.BattleRoundUI.DynamicTexture2DAtlas;
namespace Zjcs.Official.BattleRoundUI
{
using Object = UnityEngine.Object;
using UnityEngine;

public class HeadCharacterPortrait : CharacterPortraitBase
{
	[SerializeField]
	protected CharacterPortraitRequestor mRequestor;

	protected override void OnDisable()
	{
		mRequestor.StopRequest();
		base.OnDisable();
	}

	public void GeneratePortrait(long playerId, Sex sex, int profession, object appearanceData, bool needPersist = true, ProfessionPortraitKind ePortraitType = ProfessionPortraitKind.Head)
	{
		long longHashCode = ProfessionPortraitRequestKey.GetLongHashCode(playerId, 0);
		GeneratePortrait(longHashCode, sex, profession, needPersist, appearanceData, ePortraitType);
	}

	public void GenerateTemporarilyPortrait(Sex sex, int profession, object appearanceData, ProfessionPortraitKind ePortraitType = ProfessionPortraitKind.Head)
	{
		long longHashCode = ProfessionPortraitRequestKey.GetLongHashCode(appearanceData.GetHashCode(), 1);
		GeneratePortrait(longHashCode, sex, profession, needPersist: false, appearanceData, ePortraitType);
	}

	private void GeneratePortrait(long requestId, Sex sex, int profession, bool needPersist, object appearanceData, ProfessionPortraitKind ePortraitType = ProfessionPortraitKind.Head)
	{
		mRequestor.SetRequestInfo(requestId, ePortraitType, appearanceData, (int)sex, profession, needPersist);
		SetPortraitData(mRequestor);
	}
}

}
