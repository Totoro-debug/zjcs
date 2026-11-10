// Official client 884_156692; deterministic C# / logging adaptation only.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Zjcs.Official.BattleRoundUI.DynamicTexture2DAtlas
{
using Object = UnityEngine.Object;

public class GridBoxContainer
{
	private HashSet<BoxInfo> mInUseBoxes;

	private HashSet<BoxInfo> mNoUseBoxes;

	public int Width { get; private set; }

	public int Height { get; private set; }

	public int RowNum { get; private set; }

	public int ColNum { get; private set; }

	public int RowIndexMax => RowNum - 1;

	public int ColIndexMax => ColNum - 1;

	public int BoxNum { get; private set; }

	public Vector2Int GridSize { get; private set; }

	public int UsedNum => mInUseBoxes.Count;

	public int NoUseNum => mNoUseBoxes.Count;

	public GridBoxContainer(int width, int height, Vector2Int gridSize)
	{
		Width = width;
		Height = height;
		GridSize = gridSize;
		ColNum = width / gridSize.x;
		RowNum = height / gridSize.y;
		if (ColNum <= 0 || RowNum <= 0)
		{
			throw new Exception($"错误的宽高值或GridSize值 Width:{Width} Height:{Height} GridSize:{GridSize}");
		}
		BoxNum = ColNum * RowNum;
		mNoUseBoxes = new HashSet<BoxInfo>();
		mInUseBoxes = new HashSet<BoxInfo>();
		for (int num = RowNum - 1; num >= 0; num--)
		{
			for (int num2 = ColNum - 1; num2 >= 0; num2--)
			{
				int num3 = num2;
				Vector2Int gridSize2 = GridSize;
				int num4 = num3 * gridSize2.x;
				int num5 = num;
				gridSize2 = GridSize;
				int num6 = num5 * gridSize2.y;
				gridSize2 = GridSize;
				int x = gridSize2.x;
				gridSize2 = GridSize;
				RectInt rectInt = new RectInt(num4, num6, x, gridSize2.y);
				mNoUseBoxes.Add(new BoxInfo(rectInt));
			}
		}
	}

	public BoxInfo SetBoxHasUse(int x, int y)
	{
		BoxInfo tempBoxInfo = BoxInfo.GetTempBoxInfo(x, y);
		RoundAtlasDiagnostic message;
		bool enabled;
		if (mNoUseBoxes.TryGetValue(tempBoxInfo, out var actualValue))
		{
			mNoUseBoxes.Remove(tempBoxInfo);
			if (!mInUseBoxes.Add(actualValue))
			{
				message = new RoundAtlasDiagnostic(76, 0, out enabled);
				if (enabled)
				{
					message.AppendLiteral("GridBoxContainer.SetBoxHasUse 内部状态错误 从mNoUseBoxes取出来的boxInfo不能加入到mInUseBoxes");
				}
				Log.Error(ref message, null, "SetBoxHasUse", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/Base/GridBoxContainer.cs", 117);
				return null;
			}
			return actualValue;
		}
		message = new RoundAtlasDiagnostic(47, 2, out enabled);
		if (enabled)
		{
			message.AppendLiteral("GridBoxContainer.SetBoxHasUse 找不到坐标为(");
			message.AppendFormatted(x, 0, null, "x");
			message.AppendLiteral(",");
			message.AppendFormatted(y, 0, null, "y");
			message.AppendLiteral(")的BoxInfo");
		}
		Log.Error(ref message, null, "SetBoxHasUse", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/Base/GridBoxContainer.cs", 125);
		return null;
	}

	public void ResetAllBox()
	{
		foreach (BoxInfo mInUseBox in mInUseBoxes)
		{
			mNoUseBoxes.Add(mInUseBox);
		}
		mInUseBoxes.Clear();
	}

	public bool HasLeftBox(int needNum = 1)
	{
		return mNoUseBoxes.Count >= needNum;
	}

	public BoxInfo GetNoUseBox()
	{
		if (mNoUseBoxes.Count <= 0)
		{
			return null;
		}
		BoxInfo boxInfo = mNoUseBoxes.First();
		mNoUseBoxes.Remove(boxInfo);
		RoundAtlasDiagnostic message;
		bool enabled;
		if (boxInfo == null)
		{
			message = new RoundAtlasDiagnostic(41, 0, out enabled);
			if (enabled)
			{
				message.AppendLiteral("BoxContainer 内部状态错误 从mNoUseBoxes拿出了一个null");
			}
			Log.Error(ref message, null, "GetNoUseBox", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/Base/GridBoxContainer.cs", 161);
			return null;
		}
		if (!mInUseBoxes.Add(boxInfo))
		{
			message = new RoundAtlasDiagnostic(54, 0, out enabled);
			if (enabled)
			{
				message.AppendLiteral("BoxContainer 内部状态错误 从mNoUseBoxes拿出的box加入到mInUseBoxes失败");
			}
			Log.Error(ref message, null, "GetNoUseBox", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/Base/GridBoxContainer.cs", 167);
			return null;
		}
		CheckBoxNumValid();
		return boxInfo;
	}

	public bool ReleaseInUseBox(BoxInfo boxInfo)
	{
		RoundAtlasDiagnostic message;
		bool enabled;
		if (boxInfo == null)
		{
			message = new RoundAtlasDiagnostic(44, 0, out enabled);
			if (enabled)
			{
				message.AppendLiteral("BoxContainer.ReleaseInUseBox boxInfo == null");
			}
			Log.Error(ref message, null, "ReleaseInUseBox", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/Base/GridBoxContainer.cs", 183);
			return false;
		}
		if (!mInUseBoxes.Contains(boxInfo))
		{
			message = new RoundAtlasDiagnostic(88, 0, out enabled);
			if (enabled)
			{
				message.AppendLiteral("BoxContainer.ReleaseInUseBox !mInUseBoxes.Contains(boxInfo) 这个boxInfo实例不是从这个Container取出的");
			}
			Log.Error(ref message, null, "ReleaseInUseBox", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/Base/GridBoxContainer.cs", 190);
			return false;
		}
		mInUseBoxes.Remove(boxInfo);
		mNoUseBoxes.Add(boxInfo);
		CheckBoxNumValid();
		return true;
	}

	private bool CheckBoxNumValid()
	{
		if (UsedNum + NoUseNum != BoxNum)
		{
			RoundAtlasDiagnostic message = new RoundAtlasDiagnostic(77, 0, out var enabled);
			if (enabled)
			{
				message.AppendLiteral("BoxContainer.CheckBoxNumValid 返回false, 内部状态错误, (UsedNum + NoUseNum) != BoxNum");
			}
			Log.Error(ref message, null, "CheckBoxNumValid", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/Base/GridBoxContainer.cs", 208);
			return false;
		}
		return true;
	}
}
}
