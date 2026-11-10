// Official client 884_156692; deterministic C# / logging adaptation only.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.BattleRoundUI.DynamicTexture2DAtlas
{
using Object = UnityEngine.Object;

public class FallBackGridDynamicAtlas : IGridDynamicAtlas, IDisposable
{
	private Dictionary<long, GridSpriteInfo> mDicAllSprite;

	public Vector2Int AtlasSize
	{
		get
		{
			return new Vector2Int(int.MaxValue, int.MaxValue);
		}
	}

	public int SpriteNum => mDicAllSprite.Count;

	public int SpriteMaxNum => int.MaxValue;

	public Texture2D Tex => null;

	public FallBackGridDynamicAtlas()
	{
		mDicAllSprite = new Dictionary<long, GridSpriteInfo>();
	}

	public AddTexture2DResult Add(Texture2D tex, long uniqueKey)
	{
		RoundAtlasDiagnostic message;
		bool enabled;
		if ((Object)(object)tex == (Object)null)
		{
			message = new RoundAtlasDiagnostic(40, 0, out enabled);
			if (enabled)
			{
				message.AppendLiteral("FallBackGridDynamicAtlas.Add tex == null");
			}
			Log.Error(ref message, null, "Add", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/FallBackGridDynamicAtlas.cs", 29);
			return AddTexture2DResult.Failed;
		}
		if (!((Texture)tex).isReadable)
		{
			message = new RoundAtlasDiagnostic(52, 0, out enabled);
			if (enabled)
			{
				message.AppendLiteral("FallBackGridDynamicAtlas.Add tex.isReadable == false");
			}
			Log.Error(ref message, null, "Add", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/FallBackGridDynamicAtlas.cs", 35);
			return AddTexture2DResult.Failed;
		}
		if (mDicAllSprite.ContainsKey(uniqueKey))
		{
			message = new RoundAtlasDiagnostic(78, 1, out enabled);
			if (enabled)
			{
				message.AppendLiteral("FallBackGridDynamicAtlas.Add 重复的uniqueKey");
				message.AppendFormatted(uniqueKey, 0, null, "uniqueKey");
				message.AppendLiteral(" mDicAllSprite.ContainsKey(uniqueKey)");
			}
			Log.Error(ref message, null, "Add", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/FallBackGridDynamicAtlas.cs", 41);
			return AddTexture2DResult.Failed;
		}
		Texture2D val = Object.Instantiate<Texture2D>(tex);
		RectInt val2 = new RectInt(0, 0, ((Texture)tex).width, ((Texture)tex).height);
		Sprite sprite = Sprite.Create(val, val2.ToRectFloat(), new Vector2(0.5f, 0.5f), 100f, 0u, (SpriteMeshType)0);
		GridSpriteInfo gridSpriteInfo = new GridSpriteInfo(uniqueKey, null, val2, sprite, val);
		mDicAllSprite.Add(uniqueKey, gridSpriteInfo);
		return new AddTexture2DResult
		{
			SpriteInfo = gridSpriteInfo,
			Success = true,
			UniqueKey = uniqueKey
		};
	}

	public bool Remove(long uniqueKey)
	{
		GridSpriteInfo info = default;
		if (mDicAllSprite.Remove(uniqueKey, out info))
		{
			DisposeOneSpriteInfo(info);
			return true;
		}
		RoundAtlasDiagnostic message = new RoundAtlasDiagnostic(62, 1, out var enabled);
		if (enabled)
		{
			message.AppendLiteral("FallBackGridDynamicAtlas.Remove 传入的uniqueKey在容器中不存在 uniqueKey:");
			message.AppendFormatted(uniqueKey, 0, null, "uniqueKey");
		}
		Log.Error(ref message, null, "Remove", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/FallBackGridDynamicAtlas.cs", 72);
		return false;
	}

	public GridSpriteInfo Get(long uniqueKey)
	{
		if (mDicAllSprite.TryGetValue(uniqueKey, out var value))
		{
			return value;
		}
		RoundAtlasDiagnostic message = new RoundAtlasDiagnostic(39, 1, out var enabled);
		if (enabled)
		{
			message.AppendLiteral("FallBackGridDynamicAtlas.Get 获取Sprite失败");
			message.AppendFormatted(uniqueKey, 0, null, "uniqueKey");
		}
		Log.Error(ref message, null, "Get", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/FallBackGridDynamicAtlas.cs", 85);
		return null;
	}

	public bool ContainsKey(long uniqueKey)
	{
		return mDicAllSprite.ContainsKey(uniqueKey);
	}

	public void ClearAllSprites()
	{
		foreach (KeyValuePair<long, GridSpriteInfo> item in mDicAllSprite)
		{
			try
			{
				DisposeOneSpriteInfo(item.Value);
			}
			catch (Exception ex)
			{
				RoundAtlasDiagnostic message = new RoundAtlasDiagnostic(50, 2, out var enabled);
				if (enabled)
				{
					message.AppendLiteral("FallBackGridDynamicAtlas.Dispose 报错, 销毁Sprite时出错:");
					message.AppendFormatted(ex.Message, 0, null, "e.Message");
					message.AppendLiteral("\n");
					message.AppendFormatted(ex.StackTrace, 0, null, "e.StackTrace");
				}
				Log.Error(ref message, null, "ClearAllSprites", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/FallBackGridDynamicAtlas.cs", 105);
			}
		}
		mDicAllSprite.Clear();
	}

	public bool HasEnoughSpace(int needNum = 1)
	{
		return true;
	}

	public void Dispose()
	{
		ClearAllSprites();
	}

	private static void DisposeOneSpriteInfo(GridSpriteInfo info)
	{
		if (info != null)
		{
			Texture2D texAtlas = info.TexAtlas;
			info.Dispose();
			if ((Object)(object)texAtlas != (Object)null)
			{
				Object.Destroy((Object)(object)texAtlas);
			}
		}
	}
}
}
