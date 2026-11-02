using System;
namespace Zjcs.Official.World.Map13MiniMap
{
public static class MiniMapHudLanguage
{
public static string GetRegionNameKey(int mapClassId, int region)
	{
		return $"region_{mapClassId}_{region}";
	}
}
public struct MiniMapHudLanguageKey
{
public readonly string Key;
public object[] Args;
public Func<string,object[],string> Formatter;
public MiniMapHudLanguageKey(string key, params object[] args)
		{
			Key = key;
			Args = args;
			Formatter = null;
		}
public override string ToString()
		{
			return Formatter(Key, Args);
		}
}
}
