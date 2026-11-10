// Official client 884_156692; deterministic C# / logging adaptation only.
using UnityEngine.UI;

namespace Zjcs.Official.BattleRoundUI.DynamicTexture2DAtlas
{
using Object = UnityEngine.Object;

public class GridSpriteImage : Image
{
	public GridSpriteInfo GridSpriteInfo { get; private set; }

	public void SetGridSpriteInfo(GridSpriteInfo info)
	{
		if (info == null)
		{
			((Image)this).sprite = null;
			return;
		}
		GridSpriteInfo = info;
		GridSpriteInfo.OnTextureUpdated = OnSpriteUpdated;
		OnSpriteUpdated();
	}

	private void OnSpriteUpdated()
	{
		if (GridSpriteInfo != null)
		{
			((Image)this).sprite = GridSpriteInfo.Sprite;
		}
	}

	protected override void OnDestroy()
	{
		if (GridSpriteInfo != null)
		{
			GridSpriteInfo.OnTextureUpdated = null;
		}
		base.OnDestroy();
	}
}
}
