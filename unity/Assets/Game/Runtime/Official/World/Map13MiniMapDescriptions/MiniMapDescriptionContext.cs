using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeMap13Labels;
using FullScreenAction=Zjcs.Official.StoryPlayables.GAction;
namespace Zjcs.Official.World.Map13MiniMap
{
    public interface IMiniMapGridMover { bool Walkable(GroundType groundType); }
    public interface IMiniMapDescriptionAssetLease<out T>:IDisposable { T Result { get; } }
    // Exact source fields needed by minimap; LabelInfo retains the original label formula inputs.
    public sealed class MiniMapRegionInfo
    {
        public MapRegionInfo LabelInfo;public List<int> Areas;public MapRegionType RegionTag;
        public int ClassId=>LabelInfo.ClassId;public int Region=>LabelInfo.Region;public int Level=>LabelInfo.Level;public long CombatRating=>LabelInfo.CombatRating;
    }
    public sealed class MiniMapRegionDisplayInfo
    {
        public float CenterOffsetX,CenterOffsetY,HighlightScaleX,HighlightScaleY;
        public string LinePrefabPath,HighlightPrefabPath,NameFormat;
    }
    public abstract class MiniMapDescriptionContext:MiniMapSizingContext
    {
        public abstract IMap13LabelServices LabelServices { get; }
        public abstract MiniMapSettings.MiniMapSetting MiniMapSetting { get; }
        public abstract FullScreenAction ShowFullScreenAction { get; }
        public abstract bool IsFullScreen { get; }
        public abstract bool HasGridVisibilityAgent { get; }
        public abstract event Action<Pos2d,bool,bool> OnPosVisibilityUpdate;
        public abstract bool TryGetMapLiteData(int map,out MapRootEntityLiteSingleInfo info);
        public abstract IMiniMapGridMover GetSpawnGridMover(int map);
        public abstract IReadOnlyDictionary<int,MiniMapRegionInfo> GetMapRegionDict(int map);
        public abstract MiniMapRegionInfo GetRegion(int map,int region);
        public abstract int AreaToRegion(int map,int area);
        public abstract int QueryRegion(Pos2d pos);
        public abstract List<int> GetWorldMapProgresses(int map);
        public abstract MiniMapRegionDisplayInfo GetRegionDisplay(int map,int region);
        public abstract object QueryCommonGridGround(int map);
        public abstract object QuerySceneContainer(Component component);
        public abstract MiniMapRegionDescItem InstantiateDescription(MiniMapRegionDescItem prefab,Transform parent);
        public abstract GameObject InstantiateDisplay(GameObject prefab,Transform parent);
        public abstract IMiniMapDescriptionAssetLease<GameObject> LoadPrefab(string path);
        public static string GetRegionNameKey(int map,int region)=>"region_"+map+"_"+region;
        // Complete original MapUtils method; singleton lookup and IL deconstruction repaired.
	public Dictionary<int, List<Pos2d>> GetMapRegionPosList(int mapClassId)
	{
		if (!TryGetMapLiteData(mapClassId, out var liteInfo))
		{
			return null;
		}
		Dictionary<int, List<Pos2d>> dictionary = new Dictionary<int, List<Pos2d>>();
		for (int num = liteInfo.MapGroundSize.Y - 1; num >= 0; num--)
		{
			MapRootEntityLiteGridInfo[] array = liteInfo.PosDataArray[num];
			for (int num2 = liteInfo.MapGroundSize.X - 1; num2 >= 0; num2--)
			{
				MapRootEntityLiteGridInfo mapRootEntityLiteGridInfo = array[num2];
				if (mapRootEntityLiteGridInfo.Area >= 0)
				{
					if (!dictionary.TryGetValue(mapRootEntityLiteGridInfo.Area, out var value))
					{
						value = new List<Pos2d>();
						dictionary[mapRootEntityLiteGridInfo.Area] = value;
					}
					value.Add(new Pos2d(num2, num));
				}
			}
		}
		Dictionary<int, List<Pos2d>> dictionary2 = new Dictionary<int, List<Pos2d>>();
		int num3 = default;
		List<Pos2d> list = default;
		foreach (KeyValuePair<int, List<Pos2d>> item in dictionary)
		{
			num3 = item.Key;
			list = item.Value;
			int area = num3;
			List<Pos2d> collection = list;
			int key = AreaToRegion(mapClassId, area);
			if (!dictionary2.TryGetValue(key, out var value2))
			{
				value2 = (dictionary2[key] = new List<Pos2d>());
			}
			value2.AddRange(collection);
		}
		return dictionary2;
	}

    }
    internal struct MiniMapDescriptionBag:IDisposable
    {
        private List<IDisposable> values;public void Add(IDisposable value){if(values==null)values=new List<IDisposable>();values.Add(value);}
        public void Dispose(){if(values==null)return;foreach(var value in values)value.Dispose();values.Clear();}
    }
    internal static class MiniMapDescriptionDisposable { public static void AddTo<T>(T value,ref MiniMapDescriptionBag bag) where T:IDisposable=>bag.Add(value); }
}
