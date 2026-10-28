namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;

public struct PlayerInfoData
{
	public IProfessionInfoTarget TargetData;

	public string Icon;

	public string Name;

	public long CombatRating;

	public int SubRank;

	public int Level;

	public object AppearanceData;

	public int Profession;

	public Sex Sex;

	public int EngagePetClassId;

	public object NameArg;

	public long Id => TargetData.TargetId;

}

}
