// Official client 884_156692; deterministic C# / logging adaptation only.
using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Zjcs.Official.BattleRoundUI.DynamicTexture2DAtlas
{
using Object = UnityEngine.Object;

public static class DynamicTexture2DAtlasUtils
{
	private static bool sHasCheck;

	public const GraphicsFormat kETC2Fmt = (GraphicsFormat)123;

	public const GraphicsFormat kRGBA32Fmt = (GraphicsFormat)4;

	public const int kMaxAtlasWidth = 2048;

	public const int kMaxAtlasHeight = 2048;

	public static bool SupportETC2Fmt => SystemInfo.IsFormatSupported((GraphicsFormat)123, (FormatUsage)0);

	public static GraphicsFormat UseFormat
	{
		get
		{
			if (SupportETC2Fmt)
			{
				return (GraphicsFormat)123;
			}
			return (GraphicsFormat)4;
		}
	}

	public static bool CanGraphicTextureCopy
	{
		get
		{
			return (int)SystemInfo.copyTextureSupport > 0;
		}
	}

	public static int NewTextureCounter { get; private set; }

	public static int NewSpriteCounter { get; private set; }

	public static long ExtendAtlasCostTicks { get; set; }

	public static void ReportGraphicsSupportInfo()
	{
		if (sHasCheck)
		{
			return;
		}
		sHasCheck = true;
		RoundAtlasDiagnostic message;
		bool enabled;
		if (!SupportETC2Fmt)
		{
			message = new RoundAtlasDiagnostic(42, 0, out enabled);
			if (enabled)
			{
				message.AppendLiteral("ReportETC2Support: SupportETC2Fmt == false");
			}
			Log.Error(ref message, null, "ReportGraphicsSupportInfo", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/Base/DynamicTexture2DAtlasUtils.cs", 38);
		}
		if (!CanGraphicTextureCopy)
		{
			message = new RoundAtlasDiagnostic(56, 0, out enabled);
			if (enabled)
			{
				message.AppendLiteral("ReportCopyTextureSupport: CanGraphicTextureCopy == false");
			}
			Log.Error(ref message, null, "ReportGraphicsSupportInfo", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/Base/DynamicTexture2DAtlasUtils.cs", 45);
		}
	}

	public static bool IsSupportFormat(GraphicsFormat format)
	{
		if ((int)format != 123)
		{
			return (int)format == 4;
		}
		return true;
	}

	public static IGridDynamicAtlas CreateGridDynamicAtlas(int atlasWidth, int atlasHeight, Vector2Int gridSize, Action onTextureExtend, bool needCreateSprite)
	{
		return CreateGridDynamicAtlas2(atlasWidth, atlasHeight, gridSize, onTextureExtend, UseFormat, needCreateSprite, forceUseFallbackAtlas: false, 2048, 2048);
	}

	public static IGridDynamicAtlas CreateGridDynamicAtlas2(int atlasWidth, int atlasHeight, Vector2Int gridSize, Action onTextureExtend, GraphicsFormat atlasFormat, bool needCreateSprite, bool forceUseFallbackAtlas, int atlasMaxWidth, int atlasMaxHeight)
	{
		if (forceUseFallbackAtlas)
		{
			return new FallBackGridDynamicAtlas();
		}
		if (CanGraphicTextureCopy && SupportETC2Fmt)
		{
			return new ETC2GridDynamicAtlas(atlasWidth, atlasHeight, gridSize, atlasFormat, onTextureExtend, needCreateSprite, 4, atlasMaxWidth, atlasMaxHeight);
		}
		return new FallBackGridDynamicAtlas();
	}

	public static bool CopySameFormat(Texture2D srcTex, Texture2D dstTex, RectInt dstRect)
	{
		RoundAtlasDiagnostic message;
		bool enabled;
		if ((Object)(object)srcTex == (Object)null || (Object)(object)dstTex == (Object)null)
		{
			message = new RoundAtlasDiagnostic(58, 2, out enabled);
			if (enabled)
			{
				message.AppendLiteral("CopySameFormat 拷贝纹理失败, srcTex == null || dstTex == null ");
				message.AppendFormatted((Object)(object)srcTex == (Object)null, 0, null, "srcTex == null");
				message.AppendLiteral(" ");
				message.AppendFormatted((Object)(object)dstTex == (Object)null, 0, null, "dstTex == null");
				message.AppendLiteral(" ");
			}
			Log.Error(ref message, null, "CopySameFormat", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/Base/DynamicTexture2DAtlasUtils.cs", 98);
			return false;
		}
		int num = 0;
		int num2 = 0;
		int width = ((Texture)srcTex).width;
		int height = ((Texture)srcTex).height;
		int width2 = ((Texture)dstTex).width;
		int height2 = ((Texture)dstTex).height;
		if (dstRect.x + width > width2 || dstRect.y + height > height2)
		{
			message = new RoundAtlasDiagnostic(49, 3, out enabled);
			if (enabled)
			{
				message.AppendLiteral("CopySameFormat 拷贝纹理失败, 坐标不合法 dstRect:");
				message.AppendFormatted<RectInt>(dstRect, 0, null, "dstRect");
				message.AppendLiteral(" srcW:");
				message.AppendFormatted(width, 0, null, "srcW");
				message.AppendLiteral(" srcH:");
				message.AppendFormatted(height, 0, null, "srcH");
			}
			Log.Error(ref message, null, "CopySameFormat", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/Base/DynamicTexture2DAtlasUtils.cs", 112);
			return false;
		}
		try
		{
			Graphics.CopyTexture((Texture)(object)srcTex, 0, 0, num, num2, width, height, (Texture)(object)dstTex, 0, 0, dstRect.x, dstRect.y);
		}
		catch (Exception ex)
		{
			message = new RoundAtlasDiagnostic(38, 2, out enabled);
			if (enabled)
			{
				message.AppendLiteral("GridDynamicETC2Tex2DAtlas.CopySame 异常");
				message.AppendFormatted(ex.Message, 0, null, "e.Message");
				message.AppendLiteral(" ");
				message.AppendFormatted(ex.StackTrace, 0, null, "e.StackTrace");
			}
			Log.Error(ref message, null, "CopySameFormat", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/Base/DynamicTexture2DAtlasUtils.cs", 122);
			enabled = false;
			return enabled;
		}
		return true;
	}

	public static bool CopyETC2ToRGBA32(Texture2D srcTex, Texture2D dstTex, RectInt dstRect)
	{
		Texture2D val = null;
		RenderTexture active = RenderTexture.active;
		bool flag = false;
		try
		{
			val = new Texture2D(((Texture)srcTex).width, ((Texture)srcTex).height, (GraphicsFormat)4, 0, (TextureCreationFlags)0);
			Graphics.ConvertTexture((Texture)(object)srcTex, (Texture)(object)val);
			flag = true;
			CopySameFormat(val, dstTex, dstRect);
		}
		catch (Exception ex)
		{
			RoundAtlasDiagnostic message = new RoundAtlasDiagnostic(20, 2, out var enabled);
			if (enabled)
			{
				message.AppendLiteral("CopyRGBA32ToETC2异常:");
				message.AppendFormatted(ex.Message, 0, null, "e.Message");
				message.AppendLiteral("\n");
				message.AppendFormatted(ex.StackTrace, 0, null, "e.StackTrace");
			}
			Log.Error(ref message, null, "CopyETC2ToRGBA32", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/Base/DynamicTexture2DAtlasUtils.cs", 143);
			enabled = false;
			return enabled;
		}
		finally
		{
			if ((Object)(object)val != (Object)null)
			{
				Object.Destroy((Object)(object)val);
			}
			if (flag)
			{
				RenderTexture.active = active;
			}
		}
		return true;
	}

	public static bool CopyRGBA32ToETC2(Texture2D srcTex, Texture2D dstTex, RectInt dstRect)
	{
		Texture2D val = null;
		RoundAtlasDiagnostic message;
		bool enabled;
		try
		{
			val = new Texture2D(((Texture)srcTex).width, ((Texture)srcTex).height, (TextureFormat)4, false);
			val.SetPixelData<byte>(srcTex.GetPixelData<byte>(0), 0, 0);
			val.Compress(true);
			if (((Texture)val).graphicsFormat != ((Texture)dstTex).graphicsFormat)
			{
				message = new RoundAtlasDiagnostic(73, 2, out enabled);
				if (enabled)
				{
					message.AppendLiteral("CopyRGBA32ToETC2 copySrcTex.graphicsFormat != dstTex.graphicsFormat ");
					message.AppendFormatted<GraphicsFormat>(((Texture)val).graphicsFormat, 0, null, "copySrcTex.graphicsFormat");
					message.AppendLiteral(" ==> ");
					message.AppendFormatted<GraphicsFormat>(((Texture)dstTex).graphicsFormat, 0, null, "dstTex.graphicsFormat");
				}
				Log.Error(ref message, null, "CopyRGBA32ToETC2", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/Base/DynamicTexture2DAtlasUtils.cs", 168);
				enabled = false;
				return enabled;
			}
			CopySameFormat(val, dstTex, dstRect);
		}
		catch (Exception ex)
		{
			message = new RoundAtlasDiagnostic(20, 2, out var enabled2);
			if (enabled2)
			{
				message.AppendLiteral("CopyRGBA32ToETC2异常:");
				message.AppendFormatted(ex.Message, 0, null, "e.Message");
				message.AppendLiteral("\n");
				message.AppendFormatted(ex.StackTrace, 0, null, "e.StackTrace");
			}
			Log.Error(ref message, null, "CopyRGBA32ToETC2", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/Base/DynamicTexture2DAtlasUtils.cs", 176);
			enabled = false;
			return enabled;
		}
		finally
		{
			if ((Object)(object)val != (Object)null)
			{
				Object.Destroy((Object)(object)val);
			}
		}
		return true;
	}

	[Conditional("DEBUG")]
	public static void AddNewTextureCounter(int num)
	{
		NewTextureCounter += num;
	}

	[Conditional("DEBUG")]
	public static void AddNewSpriteCounter(int num)
	{
		NewSpriteCounter += num;
	}

	public static Rect ToRectFloat(this RectInt rectInt)
	{
		return new Rect((float)rectInt.x, (float)rectInt.y, (float)rectInt.width, (float)rectInt.height);
	}
}
}
