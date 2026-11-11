// Selected original player-panel implementation; typed service boundaries only.
using System;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomePlayerInfoUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
namespace Zjcs.Official.HomePlayerPanelUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
	public struct HomePanelTextKeyArgWrap
	{
		public static HomePanelTextKeyArgWrap sEmpty = new HomePanelTextKeyArgWrap("");

		public readonly string Key;

		public object[] Args;

		public bool IsValid
		{
			get
			{
				if (!(Key != sEmpty.Key))
				{
					return Args != sEmpty.Args;
				}
				return true;
			}
		}

		public bool IsEmpty => !IsValid;

		public HomePanelTextKeyArgWrap(string key, params object[] args)
		{
			Key = key;
			Args = args;
		}

		public override bool Equals(object obj)
		{
			if (!(obj is HomePanelTextKeyArgWrap textKeyArgWrap))
			{
				return false;
			}
			if (Key != textKeyArgWrap.Key)
			{
				return false;
			}
			int num = Args?.Length ?? 0;
			if (num != (textKeyArgWrap.Args?.Length ?? 0))
			{
				return false;
			}
			if (num > 0)
			{
				for (int i = 0; i < num; i++)
				{
					if (!object.Equals(Args[i], textKeyArgWrap.Args[i]))
					{
						return false;
					}
				}
			}
			return true;
		}

		public override int GetHashCode()
		{
			object[] args = Args;
			if (args != null && args.Length != 0)
			{
				int num = Key.GetHashCode();
				object[] args2 = Args;
				for (int i = 0; i < args2.Length; i++)
				{
					int hashCode = args2[i].GetHashCode();
					num ^= hashCode;
				}
				return num;
			}
			return Key.GetHashCode();
		}

		public override string ToString()
		{
			return LanguageManager.Instance.GetTextFormated(Key, Args);
		}
	}

}
