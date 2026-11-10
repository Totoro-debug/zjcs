// Official client 884_156692; deterministic C# / logging adaptation only.
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Zjcs.Official.BattleRoundUI.DynamicTexture2DAtlas
{
using Object = UnityEngine.Object;

public class BoxInfo
{
	private static BoxInfo sTempBoxInfo = new BoxInfo();

	public RectInt RectInt { get; private set; }

	public BoxInfo(RectInt rectInt)
	{
		RectInt = rectInt;
	}

	private BoxInfo()
	{
	}

	public static BoxInfo GetTempBoxInfo(int x, int y)
	{
		sTempBoxInfo.RectInt = new RectInt(x, y, 0, 0);
		return sTempBoxInfo;
	}

	public override bool Equals(object obj)
	{
		if (!(obj is BoxInfo { RectInt: var rectInt }))
		{
			return false;
		}
		int x = rectInt.x;
		RectInt rectInt2 = RectInt;
		if (x == rectInt2.x)
		{
			int y = rectInt.y;
			rectInt2 = RectInt;
			return y == rectInt2.y;
		}
		return false;
	}

	public override int GetHashCode()
	{
		RectInt rectInt = RectInt;
		int x = rectInt.x;
		rectInt = RectInt;
		return HashCode.Combine<int, int>(x, rectInt.y);
	}
}
}
