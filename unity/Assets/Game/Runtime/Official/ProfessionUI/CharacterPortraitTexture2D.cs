namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class CharacterPortraitTexture2D
{
	private const GraphicsFormat kETC2Format = (GraphicsFormat)123;

	private static bool sSupportETC2Fmt = SystemInfo.IsFormatSupported((GraphicsFormat)123, (FormatUsage)0);

	private bool mDontDestroy;

	public Texture2D Tex { get; private set; }

	public int Width { get; private set; }

	public int Height { get; private set; }

	public bool TexIsETC2Format
	{
		get
		{

			if ((Object)(object)Tex == (Object)null)
			{
				return false;
			}
			return (int)((Texture)Tex).graphicsFormat == 123;
		}
	}

	private CharacterPortraitTexture2D()
	{
	}

	public CharacterPortraitTexture2D(int width, int height, bool dontDestroy = false)
	{

		bool flag = width % 4 == 0 && height % 4 == 0;
		if (!flag)
		{
			Debug.LogWarning("人物拍照系统: width % 4 != 0 || height % 4 != 0 (");
		}
		Tex = new Texture2D(width, height, (GraphicsFormat)((sSupportETC2Fmt & flag) ? 123 : 4), 0, (TextureCreationFlags)0);
		Width = width;
		Height = height;
		mDontDestroy = dontDestroy;
	}

	public CharacterPortraitTexture2D(Texture2D tex, int width, int height, bool dontDestroy = false)
	{
		Tex = tex;
		Width = width;
		Height = height;
		mDontDestroy = dontDestroy;
	}

	public int CostMemBytes()
	{

		return (int)((((Object)(object)Tex == (Object)null) ? 0f : GetBytesPerPixel(Tex.format)) * (float)Width * (float)Height);
	}

	public void DestroyTexture()
	{
		if (!mDontDestroy && (Object)(object)Tex != (Object)null)
		{
			Object.Destroy((Object)(object)Tex);
			Tex = null;
		}
	}

	public void ReadPixelsFromRT(RenderTexture rtRead, out byte[] bsSaveImgData)
	{

		bsSaveImgData = null;

		if ((Object)(object)Tex == (Object)null)
		{
			Debug.LogError("ReadPixelsFromRT 状态非法, 无法ReadPixel数据");
			return;
		}
		if ((Object)(object)rtRead == (Object)null)
		{
			Debug.LogError("ReadPixelsFromRT 错误, rt参数是null");
			return;
		}
		Texture2D val = new Texture2D(Width, Height, (TextureFormat)4, false);
		RenderTexture active = RenderTexture.active;
		try
		{
			RenderTexture.active = rtRead;
			val.ReadPixels(new Rect(0f, 0f, (float)Width, (float)Height), 0, 0);
			if (TexIsETC2Format)
			{
				val.Compress(true);
			}
			else
			{
				val.Apply(false);
			}
			Debug.Log("frameCount:");
			NativeArray<byte> pixelData = val.GetPixelData<byte>(0);
			bsSaveImgData = pixelData.ToArray();
			Tex.SetPixelData<byte>(pixelData, 0, 0);
			Tex.Apply(false, false);
		}
		catch (Exception ex)
		{
			Debug.LogException(ex);
		}
		finally
		{
			RenderTexture.active = active;
			Object.Destroy((Object)(object)val);
		}
	}

	public void LoadImage(byte[] bsImageData)
	{

		if ((Object)(object)Tex == (Object)null)
		{
			Debug.LogError("TexturePairCharacterPortrait 状态非法, 无法LoadImage数据");
			return;
		}
		Debug.Log("头像拍照系统: 从Bytes[] 读取数据到Texture中");
		Tex.SetPixelData<byte>(bsImageData, 0, 0);
		Tex.Apply(false, false);
	}

	public static float GetBitsPerPixel(TextureFormat textureFormat)
	{

		if ((int)textureFormat - 4 > 1)
		{
			if ((int)textureFormat != 12)
			{
				switch ((int)textureFormat - 47)
				{
				case 1:
					return 8f;
				case 2:
					return 5.12f;
				case 3:
					return 3.56f;
				case 4:
					return 2f;
				case 0:
					break;
				default:
				{
					Debug.Log("GetBpp 返回32f, 其他格式还没录入..");
					return 32f;
				}
				}
			}
			return 8f;
		}
		return 32f;
	}

	public static float GetBytesPerPixel(TextureFormat textureFormat)
	{

		return GetBitsPerPixel(textureFormat) / 8f;
	}
}

}
