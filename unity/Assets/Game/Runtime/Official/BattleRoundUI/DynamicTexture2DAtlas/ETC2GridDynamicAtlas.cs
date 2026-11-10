// Official client 884_156692; deterministic C# / logging adaptation only.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Zjcs.Official.BattleRoundUI.DynamicTexture2DAtlas
{
using Object = UnityEngine.Object;

public class ETC2GridDynamicAtlas : IGridDynamicAtlas, IDisposable
{
	private GridBoxContainer mGridBoxContainer;

	private Dictionary<long, GridSpriteInfo> mDicAllSprite;

	private Texture2D mTexAtlas;

	public int NoUseBoxNum => mGridBoxContainer.NoUseNum;

	public int UseBoxNum => mGridBoxContainer.UsedNum;

	public Vector2Int AtlasSize
	{
		get
		{
			if ((Object)(object)mTexAtlas == (Object)null)
			{
				return Vector2Int.zero;
			}
			return new Vector2Int(((Texture)mTexAtlas).width, ((Texture)mTexAtlas).height);
		}
	}

	public int SpriteNum => mDicAllSprite.Count;

	public int SpriteMaxNum => mGridBoxContainer.BoxNum;

	public Texture2D Tex => mTexAtlas;

	private GraphicsFormat AtlasFormat { get; }

	private Vector2Int GridSize { get; }

	private Vector2Int BoxGridSize { get; }

	private bool NeedCreateSprite { get; }

	private int LTRBMargin { get; }

	private int AtlasMaxWidth { get; }

	private int AtlasMaxHeight { get; }

	private Action OnTextureExtended { get; }

	public ETC2GridDynamicAtlas(int atlasWidth, int atlasHeight, Vector2Int gridSize, GraphicsFormat atlasFormat, Action onTextureExtended, bool needCreateSprite, int ltrbMargin, int atlasMaxWidth, int atlasMaxHeight)
	{
		if (atlasWidth % 4 != 0 || atlasHeight % 4 != 0 || gridSize.x % 4 != 0 || gridSize.y % 4 != 0 || ltrbMargin % 4 != 0 || atlasMaxWidth % 4 != 0 || atlasMaxHeight % 4 != 0)
		{
			throw new Exception($"所有参数都必须是4的倍数 atlasWidth:{atlasWidth} atlasHeight:{atlasHeight} gridSize:{gridSize} ltrbMargin:{ltrbMargin}");
		}
		DynamicTexture2DAtlasUtils.ReportGraphicsSupportInfo();
		GridSize = gridSize;
		AtlasFormat = atlasFormat;
		OnTextureExtended = onTextureExtended;
		NeedCreateSprite = needCreateSprite;
		AtlasMaxWidth = atlasMaxWidth;
		AtlasMaxHeight = atlasMaxHeight;
		LTRBMargin = ltrbMargin;
		BoxGridSize = new Vector2Int(gridSize.x + ltrbMargin * 2, gridSize.y + ltrbMargin * 2);
		mGridBoxContainer = new GridBoxContainer(atlasWidth, atlasHeight, BoxGridSize);
		mDicAllSprite = new Dictionary<long, GridSpriteInfo>();
		mTexAtlas = CreateNewTex2D(atlasWidth, atlasHeight, atlasFormat);
		mTexAtlas.Apply(false, false);
	}

	public AddTexture2DResult Add(Texture2D tex, long uniqueKey)
	{
		RoundAtlasDiagnostic message;
		bool enabled;
		if ((Object)(object)tex == (Object)null)
		{
			message = new RoundAtlasDiagnostic(41, 0, out enabled);
			if (enabled)
			{
				message.AppendLiteral("GridDynamicETC2Tex2DAtlas.Add tex == null");
			}
			Log.Error(ref message, null, "Add", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/ETC2GridDynamicAtlas.cs", 104);
			return AddTexture2DResult.Failed;
		}
		if (!((Texture)tex).isReadable)
		{
			message = new RoundAtlasDiagnostic(53, 0, out enabled);
			if (enabled)
			{
				message.AppendLiteral("GridDynamicETC2Tex2DAtlas.Add tex.isReadable == false");
			}
			Log.Error(ref message, null, "Add", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/ETC2GridDynamicAtlas.cs", 112);
			return AddTexture2DResult.Failed;
		}
		if (((Texture)tex).width % 4 != 0 || ((Texture)tex).width % 4 != 0)
		{
			message = new RoundAtlasDiagnostic(101, 3, out enabled);
			if (enabled)
			{
				message.AppendLiteral("GridDynamicETC2Tex2DAtlas.Add tex.width % 4 != 0 || tex.width % 4 != 0 纹理分辨率必须是4的倍数 tex.name:");
				message.AppendFormatted(((Object)tex).name, 0, null, "tex.name");
				message.AppendLiteral(" 分辨率:(");
				message.AppendFormatted(((Texture)tex).width, 0, null, "tex.width");
				message.AppendLiteral(",");
				message.AppendFormatted(((Texture)tex).height, 0, null, "tex.height");
				message.AppendLiteral(")");
			}
			Log.Error(ref message, null, "Add", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/ETC2GridDynamicAtlas.cs", 118);
			return AddTexture2DResult.Failed;
		}
		if (mDicAllSprite.ContainsKey(uniqueKey))
		{
			message = new RoundAtlasDiagnostic(79, 1, out enabled);
			if (enabled)
			{
				message.AppendLiteral("GridDynamicETC2Tex2DAtlas.Add 重复的uniqueKey");
				message.AppendFormatted(uniqueKey, 0, null, "uniqueKey");
				message.AppendLiteral(" mDicAllSprite.ContainsKey(uniqueKey)");
			}
			Log.Error(ref message, null, "Add", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/ETC2GridDynamicAtlas.cs", 124);
			return AddTexture2DResult.Failed;
		}
		if (!DynamicTexture2DAtlasUtils.IsSupportFormat(((Texture)tex).graphicsFormat))
		{
			message = new RoundAtlasDiagnostic(49, 2, out enabled);
			if (enabled)
			{
				message.AppendLiteral("GridDynamicETC2Tex2DAtlas.Add 不支持的纹理格式:");
				message.AppendFormatted<GraphicsFormat>(((Texture)tex).graphicsFormat, 0, null, "tex.graphicsFormat");
				message.AppendLiteral(" tex.name:");
				message.AppendFormatted(((Object)tex).name, 0, null, "tex.name");
			}
			Log.Error(ref message, null, "Add", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/ETC2GridDynamicAtlas.cs", 130);
			return AddTexture2DResult.Failed;
		}
		if (!mGridBoxContainer.HasLeftBox())
		{
			if (!TryExtendAtlas())
			{
				message = new RoundAtlasDiagnostic(36, 0, out enabled);
				if (enabled)
				{
					message.AppendLiteral("GridDynamicETC2Tex2DAtlas.Add 图集空间不够");
				}
				Log.Error(ref message, null, "Add", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/ETC2GridDynamicAtlas.cs", 146);
				return AddTexture2DResult.Failed;
			}
			if (!mGridBoxContainer.HasLeftBox())
			{
				message = new RoundAtlasDiagnostic(43, 0, out enabled);
				if (enabled)
				{
					message.AppendLiteral("GridDynamicETC2Tex2DAtlas.Add 扩容后, 图集空间还是不够");
				}
				Log.Error(ref message, null, "Add", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/ETC2GridDynamicAtlas.cs", 153);
				return AddTexture2DResult.Failed;
			}
		}
		BoxInfo noUseBox = mGridBoxContainer.GetNoUseBox();
		if (noUseBox == null)
		{
			message = new RoundAtlasDiagnostic(68, 0, out enabled);
			if (enabled)
			{
				message.AppendLiteral("GridDynamicETC2Tex2DAtlas.Add mBoxContainer.GetNoUseBox()返回空的boxInfo");
			}
			Log.Error(ref message, null, "Add", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/ETC2GridDynamicAtlas.cs", 162);
			return AddTexture2DResult.Failed;
		}
		RectInt rectInt = noUseBox.RectInt;
		int x = rectInt.x;
		rectInt = noUseBox.RectInt;
		Vector2Int val = ConvertBoxPos2RectPos(x, rectInt.y, LTRBMargin);
		int width = ((Texture)tex).width;
		int height = ((Texture)tex).height;
		RectInt val2 = new RectInt(val.x, val.y, width, height);
		bool flag = false;
		try
		{
			flag = CopyTexture(tex, mTexAtlas, val2);
		}
		catch (Exception ex)
		{
			message = new RoundAtlasDiagnostic(36, 2, out enabled);
			if (enabled)
			{
				message.AppendLiteral("GridDynamicETC2Tex2DAtlas.Add 异常发生:");
				message.AppendFormatted(ex.Message, 0, null, "e.Message");
				message.AppendLiteral("\b");
				message.AppendFormatted(ex.StackTrace, 0, null, "e.StackTrace");
			}
			Log.Error(ref message, null, "Add", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/ETC2GridDynamicAtlas.cs", 177);
			return AddTexture2DResult.Failed;
		}
		if (!flag)
		{
			message = new RoundAtlasDiagnostic(41, 0, out enabled);
			if (enabled)
			{
				message.AppendLiteral("GridDynamicETC2Tex2DAtlas.Add 拷贝Texture失败");
			}
			Log.Error(ref message, null, "Add", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/ETC2GridDynamicAtlas.cs", 183);
			return AddTexture2DResult.Failed;
		}
		Sprite sprite = CreateNewSprite(rectInAtlas: new RectInt(val.x + 1, val.y + 1, width - 2, height - 2), texAtlas: mTexAtlas, needCreateSprite: NeedCreateSprite);
		GridSpriteInfo gridSpriteInfo = new GridSpriteInfo(uniqueKey, noUseBox, val2, sprite, mTexAtlas);
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
		GridSpriteInfo gridSpriteInfo = default;
		RoundAtlasDiagnostic message;
		bool enabled;
		if (mDicAllSprite.Remove(uniqueKey, out gridSpriteInfo))
		{
			bool flag = mGridBoxContainer.ReleaseInUseBox(gridSpriteInfo.BoxInfo);
			gridSpriteInfo.Dispose();
			if (!flag)
			{
				message = new RoundAtlasDiagnostic(65, 0, out enabled);
				if (enabled)
				{
					message.AppendLiteral("GridDynamicETC2Tex2DAtlas.Remove mBoxContainer.ReleaseInUseBox失败 ");
				}
				Log.Error(ref message, null, "Remove", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/ETC2GridDynamicAtlas.cs", 209);
			}
			return flag;
		}
		message = new RoundAtlasDiagnostic(63, 1, out enabled);
		if (enabled)
		{
			message.AppendLiteral("GridDynamicETC2Tex2DAtlas.Remove 传入的uniqueKey在容器中不存在 uniqueKey:");
			message.AppendFormatted(uniqueKey, 0, null, "uniqueKey");
		}
		Log.Error(ref message, null, "Remove", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/ETC2GridDynamicAtlas.cs", 216);
		return false;
	}

	public GridSpriteInfo Get(long uniqueKey)
	{
		if (mDicAllSprite.TryGetValue(uniqueKey, out var value))
		{
			return value;
		}
		RoundAtlasDiagnostic message = new RoundAtlasDiagnostic(40, 1, out var enabled);
		if (enabled)
		{
			message.AppendLiteral("GridDynamicETC2Tex2DAtlas.Get 获取Sprite失败");
			message.AppendFormatted(uniqueKey, 0, null, "uniqueKey");
		}
		Log.Error(ref message, null, "Get", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/ETC2GridDynamicAtlas.cs", 229);
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
				item.Value.Dispose();
			}
			catch (Exception ex)
			{
				RoundAtlasDiagnostic message = new RoundAtlasDiagnostic(51, 2, out var enabled);
				if (enabled)
				{
					message.AppendLiteral("GridDynamicETC2Tex2DAtlas.Dispose 报错, 销毁Sprite时出错:");
					message.AppendFormatted(ex.Message, 0, null, "e.Message");
					message.AppendLiteral("\n");
					message.AppendFormatted(ex.StackTrace, 0, null, "e.StackTrace");
				}
				Log.Error(ref message, null, "ClearAllSprites", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/ETC2GridDynamicAtlas.cs", 249);
			}
		}
		mDicAllSprite.Clear();
		mGridBoxContainer.ResetAllBox();
	}

	public bool HasEnoughSpace(int needNum = 1)
	{
		return mGridBoxContainer.HasLeftBox(needNum);
	}

	public void Dispose()
	{
		ClearAllSprites();
		if ((Object)(object)mTexAtlas != (Object)null)
		{
			Object.Destroy((Object)(object)mTexAtlas);
			mTexAtlas = null;
		}
	}

	private bool TryExtendAtlas()
	{
		int width = ((Texture)mTexAtlas).width;
		int height = ((Texture)mTexAtlas).height;
		RoundAtlasDiagnostic message;
		bool enabled;
		if (width >= AtlasMaxWidth && height >= AtlasMaxHeight)
		{
			message = new RoundAtlasDiagnostic(86, 4, out enabled);
			if (enabled)
			{
				message.AppendLiteral("TryExtendAtlas 图集扩容失败 curW >= mAtlasMaxWidth || curH >= mAtlasMaxHeight 最大:(");
				message.AppendFormatted(AtlasMaxWidth, 0, null, "AtlasMaxWidth");
				message.AppendLiteral(",");
				message.AppendFormatted(AtlasMaxHeight, 0, null, "AtlasMaxHeight");
				message.AppendLiteral(") Cur:(");
				message.AppendFormatted(width, 0, null, "curW");
				message.AppendLiteral(",");
				message.AppendFormatted(height, 0, null, "curH");
				message.AppendLiteral(")");
			}
			Log.Error(ref message, null, "TryExtendAtlas", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/ETC2GridDynamicAtlas.cs", 288);
			return false;
		}
		int retNum = width;
		int retNum2 = height;
		FindNextPowerOfTwo(width, out retNum);
		if (retNum > AtlasMaxWidth)
		{
			retNum = width;
			FindNextPowerOfTwo(height, out retNum2);
			if (retNum2 > AtlasMaxHeight)
			{
				message = new RoundAtlasDiagnostic(46, 4, out enabled);
				if (enabled)
				{
					message.AppendLiteral("TryExtendAtlas 图集扩容失败, 找不到合适的宽高 最大:(");
					message.AppendFormatted(AtlasMaxWidth, 0, null, "AtlasMaxWidth");
					message.AppendLiteral(",");
					message.AppendFormatted(AtlasMaxHeight, 0, null, "AtlasMaxHeight");
					message.AppendLiteral(") Cur:(");
					message.AppendFormatted(width, 0, null, "curW");
					message.AppendLiteral(",");
					message.AppendFormatted(height, 0, null, "curH");
					message.AppendLiteral(")");
				}
				Log.Error(ref message, null, "TryExtendAtlas", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/ETC2GridDynamicAtlas.cs", 302);
				return false;
			}
		}
		Texture2D val = mTexAtlas;
		Texture2D val2 = (mTexAtlas = CreateNewTex2D(retNum, retNum2, AtlasFormat));
		GridBoxContainer gridBoxContainer = (mGridBoxContainer = new GridBoxContainer(retNum, retNum2, BoxGridSize));
		Graphics.CopyTexture((Texture)(object)val, 0, 0, 0, 0, ((Texture)val).width, ((Texture)val).height, (Texture)(object)val2, 0, 0, 0, 0);
		foreach (KeyValuePair<long, GridSpriteInfo> item in mDicAllSprite)
		{
			GridSpriteInfo value = item.Value;
			BoxInfo boxInfo = value.BoxInfo;
			RectInt rectInt = boxInfo.RectInt;
			int x = rectInt.x;
			rectInt = boxInfo.RectInt;
			BoxInfo boxInfo2 = gridBoxContainer.SetBoxHasUse(x, rectInt.y);
			if (boxInfo2 == null)
			{
				message = new RoundAtlasDiagnostic(52, 0, out enabled);
				if (enabled)
				{
					message.AppendLiteral("ETC2GridDynamicAtlas.TryExtendAtlas 扩张图集时,Sprite转移失败");
				}
				Log.Error(ref message, null, "TryExtendAtlas", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/ETC2GridDynamicAtlas.cs", 335);
				continue;
			}
			RectInt rectInAtlas = value.RectInAtlas;
			rectInt = boxInfo2.RectInt;
			int x2 = rectInt.x;
			rectInt = boxInfo2.RectInt;
			Vector2Int val3 = ConvertBoxPos2RectPos(x2, rectInt.y, LTRBMargin);
			Vector2Int size = rectInAtlas.size;
			RectInt rectInAtlas2 = new RectInt(val3.x, val3.y, size.x, size.y);
			try
			{
				Sprite sprite = CreateNewSprite(val2, rectInAtlas2, NeedCreateSprite);
				value.OnAtlasTextureChanged(boxInfo2, rectInAtlas2, sprite, val2, needCallback: true);
			}
			catch (Exception ex)
			{
				message = new RoundAtlasDiagnostic(42, 2, out enabled);
				if (enabled)
				{
					message.AppendLiteral("ETC2GridDynamicAtlas.TryExtendAtlas 发生异常:");
					message.AppendFormatted(ex.Message, 0, null, "e.Message");
					message.AppendLiteral("\n");
					message.AppendFormatted(ex.StackTrace, 0, null, "e.StackTrace");
				}
				Log.Error(ref message, null, "TryExtendAtlas", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/ETC2GridDynamicAtlas.cs", 351);
			}
		}
		try
		{
			OnTextureExtended?.Invoke();
		}
		catch (Exception ex2)
		{
			message = new RoundAtlasDiagnostic(61, 2, out enabled);
			if (enabled)
			{
				message.AppendLiteral("ETC2GridDynamicAtlas.TryExtendAtlas 回调mOnTextureExtended时异常:");
				message.AppendFormatted(ex2.Message, 0, null, "e.Message");
				message.AppendLiteral("\n");
				message.AppendFormatted(ex2.StackTrace, 0, null, "e.StackTrace");
			}
			Log.Error(ref message, null, "TryExtendAtlas", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/ETC2GridDynamicAtlas.cs", 361);
		}
		Object.Destroy((Object)(object)val);
		return true;
	}

	private static Vector2Int ConvertBoxPos2RectPos(int boxPosX, int boxPosY, int margin)
	{
		return new Vector2Int(boxPosX + margin, boxPosY + margin);
	}

	private static Texture2D CreateNewTex2D(int atlasWidth, int atlasHeight, GraphicsFormat atlasFormat)
	{
		Texture2D val = new Texture2D(atlasWidth, atlasHeight, atlasFormat, 0, (TextureCreationFlags)0)
		{
			filterMode = (FilterMode)1
		};
		((Object)val).name = string.Format("{0}_({1},{2})_{3}", "ETC2GridDynamicAtlas", atlasWidth, atlasHeight, atlasFormat);
		return val;
	}

	private static Sprite CreateNewSprite(Texture2D texAtlas, RectInt rectInAtlas, bool needCreateSprite)
	{
		if (!needCreateSprite)
		{
			return null;
		}
		Sprite val = Sprite.Create(texAtlas, rectInAtlas.ToRectFloat(), new Vector2(0.5f, 0.5f), 100f, 0u, (SpriteMeshType)0);
		((Object)val).name = string.Format("{0}_sprite_({1},{2})", "ETC2GridDynamicAtlas", rectInAtlas.width, rectInAtlas.height);
		return val;
	}

	public static bool FindNextPowerOfTwo(int num, out int retNum)
	{
		if (num <= 0)
		{
			retNum = 0;
			return true;
		}
		if (num == 1)
		{
			retNum = 2;
			return true;
		}
		int num2 = 1;
		int num3 = num * 2;
		for (int i = 0; i < 31; i++)
		{
			if (num2 >= num3)
			{
				retNum = num2;
				return true;
			}
			num2 <<= 1;
		}
		retNum = 0;
		return false;
	}

	private static bool CopyTexture(Texture2D srcTex, Texture2D dstTex, RectInt dstRect)
	{
		RoundAtlasDiagnostic message;
		bool enabled;
		if ((Object)(object)srcTex == (Object)null || (Object)(object)dstTex == (Object)null)
		{
			message = new RoundAtlasDiagnostic(44, 0, out enabled);
			if (enabled)
			{
				message.AppendLiteral("CopyTexture srcTex == null || dstTex == null");
			}
			Log.Error(ref message, null, "CopyTexture", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/ETC2GridDynamicAtlas.cs", 441);
			return false;
		}
		GraphicsFormat graphicsFormat = ((Texture)srcTex).graphicsFormat;
		GraphicsFormat graphicsFormat2 = ((Texture)dstTex).graphicsFormat;
		if (graphicsFormat == graphicsFormat2)
		{
			return DynamicTexture2DAtlasUtils.CopySameFormat(srcTex, dstTex, dstRect);
		}
		if ((int)graphicsFormat == 123 && (int)graphicsFormat2 == 4)
		{
			return DynamicTexture2DAtlasUtils.CopyETC2ToRGBA32(srcTex, dstTex, dstRect);
		}
		if ((int)graphicsFormat == 4 && (int)graphicsFormat2 == 123)
		{
			return DynamicTexture2DAtlasUtils.CopyRGBA32ToETC2(srcTex, dstTex, dstRect);
		}
		message = new RoundAtlasDiagnostic(65, 2, out enabled);
		if (enabled)
		{
			message.AppendLiteral("GridDynamicETC2Tex2DAtlas, 不应该走到这个逻辑, 未知的fmt组合: srcFmt:");
			message.AppendFormatted<GraphicsFormat>(graphicsFormat, 0, null, "srcFmt");
			message.AppendLiteral(" + dstFmt:");
			message.AppendFormatted<GraphicsFormat>(graphicsFormat2, 0, null, "dstFmt");
		}
		Log.Error(ref message, null, "CopyTexture", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/ETC2GridDynamicAtlas.cs", 463);
		return false;
	}
}
}
