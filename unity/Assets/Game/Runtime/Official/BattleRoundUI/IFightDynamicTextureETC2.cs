// Official client 884_156692; original request/handler control flow.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.BattleRoundUI.DynamicTexture2DAtlas;
namespace Zjcs.Official.BattleRoundUI
{
using Object = UnityEngine.Object;
using UnityEngine;

public interface IFightDynamicTextureETC2
{
	AddTexture2DResult Add(Texture2D tex, long uniqueKey);

	bool Remove(long uniqueKey);

	GridSpriteInfo Get(long uniqueKey);

	GridSpriteInfo TryGetOrAdd(Texture2D tex, long uniqueKey);

	bool Contains(long uniqueKey);

	void ClearAllSprites();
}

}
