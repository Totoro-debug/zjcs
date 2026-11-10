// Official client 884_156692; deterministic C# / logging adaptation only.
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Zjcs.Official.BattleRoundUI.DynamicTexture2DAtlas
{
using Object = UnityEngine.Object;

public class GridSpriteInfo : IDisposable
{
	public long UniqueKey { get; }

	public BoxInfo BoxInfo { get; private set; }

	public Sprite Sprite { get; private set; }

	public Texture2D TexAtlas { get; private set; }

	public RectInt RectInAtlas { get; private set; }

	public bool HasSprite => (Object)(object)Sprite != (Object)null;

	public Vector2 UVLB { get; private set; }

	public Vector2 UVLT { get; private set; }

	public Vector2 UVRT { get; private set; }

	public Vector2 UVRB { get; private set; }

	public Action OnTextureUpdated { get; set; }

	public Action OnDisposed { get; set; }

	public GridSpriteInfo(long uniqueKey, BoxInfo boxInfo, RectInt rectInAtlas, Sprite sprite, Texture2D texAtlas)
	{
		UVLB = Vector2.zero;
		UVLT = Vector2.zero;
		UVRT = Vector2.zero;
		UVRB = Vector2.zero;
		UniqueKey = uniqueKey;
		OnAtlasTextureChanged(boxInfo, rectInAtlas, sprite, texAtlas, needCallback: false);
	}

	internal void OnAtlasTextureChanged(BoxInfo boxInfo, RectInt rectInAtlas, Sprite sprite, Texture2D texAtlas, bool needCallback)
	{
		Sprite sprite2 = Sprite;
		BoxInfo = boxInfo;
		RectInAtlas = rectInAtlas;
		Sprite = sprite;
		TexAtlas = texAtlas;
		UpdateUVs(rectInAtlas);
		if ((Object)(object)sprite2 != (Object)null)
		{
			Object.Destroy((Object)(object)sprite2);
		}
		if (!needCallback)
		{
			return;
		}
		try
		{
			OnTextureUpdated?.Invoke();
		}
		catch (Exception ex)
		{
			RoundAtlasDiagnostic message = new RoundAtlasDiagnostic(41, 2, out var enabled);
			if (enabled)
			{
				message.AppendLiteral("GridSpriteInfo.OnAtlasTextureChanged 异常:");
				message.AppendFormatted(ex.Message, 0, null, "e.Message");
				message.AppendLiteral("\n");
				message.AppendFormatted(ex.StackTrace, 0, null, "e.StackTrace");
			}
			Log.Error(ref message, null, "OnAtlasTextureChanged", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/Base/GridSpriteInfo.cs", 69);
		}
	}

	private void UpdateUVs(RectInt rectInAtlas)
	{
		int width = ((Texture)TexAtlas).width;
		int height = ((Texture)TexAtlas).height;
		int x = rectInAtlas.x;
		int y = rectInAtlas.y;
		int width2 = rectInAtlas.width;
		int height2 = rectInAtlas.height;
		float num = (float)x / (float)width;
		float num2 = (float)y / (float)height;
		float num3 = (float)(x + width2) / (float)width;
		float num4 = (float)(y + height2) / (float)height;
		UVLB = new Vector2(num, num2);
		UVLT = new Vector2(num, num4);
		UVRT = new Vector2(num3, num4);
		UVRB = new Vector2(num3, num2);
	}

	public void Dispose()
	{
		if ((Object)(object)Sprite != (Object)null)
		{
			OnTextureUpdated = null;
			Object.Destroy((Object)(object)Sprite);
			Sprite = null;
		}
		try
		{
			OnDisposed?.Invoke();
		}
		catch (Exception ex)
		{
			RoundAtlasDiagnostic message = new RoundAtlasDiagnostic(40, 2, out var enabled);
			if (enabled)
			{
				message.AppendLiteral("GridSpriteInfo.Dispose 调用OnDisposed时异常:");
				message.AppendFormatted(ex.Message, 0, null, "e.Message");
				message.AppendLiteral("\n");
				message.AppendFormatted(ex.StackTrace, 0, null, "e.StackTrace");
			}
			Log.Error(ref message, null, "Dispose", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/Base/GridSpriteInfo.cs", 158);
		}
	}
}
}
