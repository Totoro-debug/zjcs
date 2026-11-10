// Official client 884_156692; deterministic C# / logging adaptation only.
using System;
using UnityEngine;

namespace Zjcs.Official.BattleRoundUI.DynamicTexture2DAtlas
{
using Object = UnityEngine.Object;

public interface IGridDynamicAtlas : IDisposable
{
	Vector2Int AtlasSize { get; }

	int SpriteNum { get; }

	int SpriteMaxNum { get; }

	Texture2D Tex { get; }

	AddTexture2DResult Add(Texture2D tex, long uniqueKey);

	bool Remove(long uniqueKey);

	GridSpriteInfo Get(long uniqueKey);

	bool ContainsKey(long uniqueKey);

	void ClearAllSprites();

	bool HasEnoughSpace(int needNum = 1);
}
}
