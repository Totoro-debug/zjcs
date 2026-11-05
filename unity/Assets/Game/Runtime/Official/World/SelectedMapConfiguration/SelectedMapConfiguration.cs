#if ZJCS_SELECTED_MAP_CONFIGURATION
using System.Collections.Generic;
using Common;
using Luban;
namespace Zjcs.Mainline.SelectedConfiguration {
public sealed class SelectedMapWorldInfo : MapWorldInfo {
 public readonly bool SupportFastClear;
 public readonly string EnterNavigateKey;
 public SelectedMapWorldInfo(ByteBuf buf) : base(buf) { SupportFastClear=buf.ReadBool(); EnterNavigateKey=buf.ReadString(); }
 public SelectedMapWorldInfo(ByteBuf buf, Dictionary<int,List<int>>[] rewards, HashSet<int>[] treasures) : base(buf,rewards,treasures) { SupportFastClear=buf.ReadBool(); EnterNavigateKey=buf.ReadString(); }
}
public sealed class SelectedEntityExtendParameterInfo : EntityExtendParameterInfo {
 public bool IgnoreFastClear;
 public SelectedEntityExtendParameterInfo(ByteBuf buf) : base(buf) { IgnoreFastClear=buf.ReadBool(); }
}
public sealed class SelectedMapWorldInfoParser : MapWorldInfoParser {
 protected override IReadOnlyDictionary<int,MapWorldInfo> LoadLubanTable(ByteBuf buf) => new SelectedTbMapWorldInfo(buf).DataMap;
}
public sealed class SelectedEntityExtendParameterInfoParser : EntityExtendParameterInfoParser {
 protected override IReadOnlyDictionary<int,EntityExtendParameterInfo> LoadLubanTable(ByteBuf buf) => new SelectedTbEntityExtendParameterInfo(buf).DataMap;
}




public class SelectedTbMapWorldInfo
{
	private readonly Dictionary<int, MapWorldInfo> _dataMap;

	private readonly List<MapWorldInfo> _dataList;

	public IReadOnlyDictionary<int, MapWorldInfo> DataMap => _dataMap;

	public IReadOnlyList<MapWorldInfo> DataList => _dataList;

	public MapWorldInfo this[int key] => _dataMap[key];

	public SelectedTbMapWorldInfo(ByteBuf _buf)
	{
		int num = _buf.ReadSize();
		int num2 = _buf.ReadSize();
		Dictionary<int, List<int>>[] array = new Dictionary<int, List<int>>[num2 + 1];
		for (int i = 1; i <= num2; i++)
		{
			int num3 = _buf.ReadSize();
			Dictionary<int, List<int>> dictionary = new Dictionary<int, List<int>>(num3 * 3 / 2);
			for (int j = 0; j < num3; j++)
			{
				int key = _buf.ReadInt();
				int num4 = _buf.ReadSize();
				List<int> list = new List<int>(num4);
				for (int k = 0; k < num4; k++)
				{
					int item = _buf.ReadInt();
					list.Add(item);
				}
				dictionary.Add(key, list);
			}
			array[i] = dictionary;
		}
		int num5 = _buf.ReadSize();
		HashSet<int>[] array2 = new HashSet<int>[num5 + 1];
		for (int l = 1; l <= num5; l++)
		{
			int num6 = _buf.ReadSize();
			HashSet<int> hashSet = new HashSet<int>();
			for (int m = 0; m < num6; m++)
			{
				int item2 = _buf.ReadInt();
				hashSet.Add(item2);
			}
			array2[l] = hashSet;
		}
		_dataMap = new Dictionary<int, MapWorldInfo>(num);
		_dataList = new List<MapWorldInfo>(num);
		for (int num7 = num; num7 > 0; num7--)
		{
			MapWorldInfo mapWorldInfo = new SelectedMapWorldInfo(_buf, array, array2);
			_dataList.Add(mapWorldInfo);
			_dataMap.Add(mapWorldInfo.ClassId, mapWorldInfo);
		}
	}

	public MapWorldInfo GetOrDefault(int key)
	{
		if (!_dataMap.TryGetValue(key, out var value))
		{
			return null;
		}
		return value;
	}

	public MapWorldInfo Get(int key)
	{
		return _dataMap[key];
	}

	public void ResolveRef(Tables tables)
	{
		foreach (MapWorldInfo data in _dataList)
		{
			data.ResolveRef(tables);
		}
	}
}




public class SelectedTbEntityExtendParameterInfo
{
	private readonly Dictionary<int, EntityExtendParameterInfo> _dataMap;

	private readonly List<EntityExtendParameterInfo> _dataList;

	public IReadOnlyDictionary<int, EntityExtendParameterInfo> DataMap => _dataMap;

	public IReadOnlyList<EntityExtendParameterInfo> DataList => _dataList;

	public EntityExtendParameterInfo this[int key] => _dataMap[key];

	public SelectedTbEntityExtendParameterInfo(ByteBuf _buf)
	{
		int num = _buf.ReadSize();
		_dataMap = new Dictionary<int, EntityExtendParameterInfo>(num);
		_dataList = new List<EntityExtendParameterInfo>(num);
		for (int num2 = num; num2 > 0; num2--)
		{
			EntityExtendParameterInfo entityExtendParameterInfo = new SelectedEntityExtendParameterInfo(_buf);
			_dataList.Add(entityExtendParameterInfo);
			_dataMap.Add(entityExtendParameterInfo.EntityClassId, entityExtendParameterInfo);
		}
	}

	public EntityExtendParameterInfo GetOrDefault(int key)
	{
		if (!_dataMap.TryGetValue(key, out var value))
		{
			return null;
		}
		return value;
	}

	public EntityExtendParameterInfo Get(int key)
	{
		return _dataMap[key];
	}

	public void ResolveRef(Tables tables)
	{
		foreach (EntityExtendParameterInfo data in _dataList)
		{
			data.ResolveRef(tables);
		}
	}
}

}
#endif
