// Official client 884_156692; deterministic C# / logging adaptation only.
namespace Zjcs.Official.BattleRoundUI.DynamicTexture2DAtlas
{
using Object = UnityEngine.Object;

public struct AddTexture2DResult
{
	public bool Success;

	public long UniqueKey;

	public GridSpriteInfo SpriteInfo;

	public static AddTexture2DResult Failed => new AddTexture2DResult
	{
		UniqueKey = 0L,
		Success = false,
		SpriteInfo = null
	};
}
}
