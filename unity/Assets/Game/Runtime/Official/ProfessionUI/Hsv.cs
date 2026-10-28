namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;

[Serializable]
public struct Hsv
{
		public int H;

		public int S;

		public int V;

	public Hsv(int h, int s, int v) { H=h; S=s; V=v; }

	public static bool operator ==(Hsv h1, Hsv h2)
	{
		if (h1.H == h2.H && h1.S == h2.S)
		{
			return h1.V == h2.V;
		}
		return false;
	}

	public static bool operator !=(Hsv h1, Hsv h2)
	{
		if (h1.H == h2.H && h1.S == h2.S)
		{
			return h1.V != h2.V;
		}
		return true;
	}

	public bool Equals(Hsv other)
	{
		if (H == other.H && S == other.S)
		{
			return V == other.V;
		}
		return false;
	}

	public override bool Equals(object obj)
	{
		if (obj is Hsv other)
		{
			return Equals(other);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return (((H * 397) ^ S) * 397) ^ V;
	}
}

}
