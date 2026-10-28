using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
public class MiniMapSettings : ScriptableObject
{
	[Serializable]
	public class MiniMapSetting
	{
		[Serializable]
		public class MiniMapRegionDescSetting
		{
			[Header("放大阈值（0 - 1）")]
			public float ZoomThreshold;

			[Header("区域虚线用的Prefab")]
			public GameObject DashedLinePrefab;
		}

		[Serializable]
		public class MiniMapCameraControllerSetting
		{
			[Header("摄像机最小范围")]
			public float MinCameraOrthographicSize;

			[Header("摄像机最大范围")]
			public float MaxCameraOrthographicSize;
		}

		public int MinimapConfigId;

		[Header("配点描述方便记忆")]
		public string MinimapConfigDesc;

		[Header("右上角小地图摄像机范围")]
		public float MiniMapUICameraOrthographicSize;

		[Header("小地图左右黑边大小（屏幕单位）")]
		public float BoundExtendSize;

		[Header("小地图物件放大阈值（0 - 1）")]
		public float MarkZoomThreshold;

		[Header("显示切换地图标签")]
		public bool ShowMapSwitch;

		[Header("全屏查看小地图时的配置")]
		public MiniMapCameraControllerSetting CameraControllerSetting;

		[Header("区域名&探索度配置")]
		public MiniMapRegionDescSetting DescSetting;
	}

	[Serializable]
	public class MiniMapTileSetting
	{
		[Serializable]
		public class AreaTagTileData
		{
			[Header("不配就是默认")]
			public string AreaTag;

			public List<GroundTypeTileData> GroundTypeTileDatas;
		}

		[Serializable]
		public class GroundTypeTileData
		{
			public GroundType GroundType;

			public Tile Tile;
		}

		[Header("以下地图会使用同一套配置")]
		public List<int> MapClassIds;

		public List<AreaTagTileData> AreaTileDatas;
	}

	[Header("全屏查看小地图时的图片质量（0 - 1）")]
	public float MiniMapFullScreenTextureQuality;

	public List<MiniMapSetting> Settings;

	public List<MiniMapTileSetting> TileSettings;

	private Dictionary<int, MiniMapSetting> mSettingDict;

	private Dictionary<int, Dictionary<string, Dictionary<GroundType, Tile>>> mTileSettingDict;

	private void OnValidate()
	{
		mSettingDict = null;
		mTileSettingDict = null;
	}

	public MiniMapSetting GetMinimapSetting(int minimapConfigId)
	{
		if (mSettingDict == null)
		{
			RefreshDict();
		}
		if (mSettingDict.TryGetValue(minimapConfigId, out var value))
		{
			return value;
		}
		return null;
	}

	private void RefreshDict()
	{
		mSettingDict = new Dictionary<int, MiniMapSetting>();
		foreach (MiniMapSetting setting in Settings)
		{
			mSettingDict.Add(setting.MinimapConfigId, setting);
		}
	}

	public Tile GetTile(int mapClassId, string areaTag, GroundType groundType)
	{
		if (mTileSettingDict == null)
		{
			RefreshTileDict();
		}
		MiniMapSettingsErrorHandler message;
		bool enabled;
		if (!mTileSettingDict.TryGetValue(mapClassId, out var value))
		{
			message = new MiniMapSettingsErrorHandler(39, 1, out enabled);
			if (enabled)
			{
				message.AppendLiteral("MiniMapSettings这个SO资源没有配地图[");
				message.AppendFormatted(mapClassId, 0, null, "mapClassId");
				message.AppendLiteral("]下的小地图tile规则");
			}
			MiniMapSettingsDiagnostic.Error(ref message, null, "GetTile", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/Console/Config/MiniMapSettings.cs", 120);
			return null;
		}
		if (!string.IsNullOrEmpty(areaTag))
		{
			Tile val = GetTile(value, areaTag, groundType);
			if ((Object)(object)val != (Object)null)
			{
				return val;
			}
		}
		areaTag = "IgnoreKey";
		Tile val2 = GetTile(value, areaTag, groundType);
		if ((Object)(object)val2 == (Object)null)
		{
			message = new MiniMapSettingsErrorHandler(65, 3, out enabled);
			if (enabled)
			{
				message.AppendLiteral("MiniMapSettings这个SO资源在地图[");
				message.AppendFormatted(mapClassId, 0, null, "mapClassId");
				message.AppendLiteral("]的AreaTag[");
				message.AppendFormatted(areaTag, 0, null, "areaTag");
				message.AppendLiteral("]以及默认AreaTag下的配置规则中不存在地形为[");
				message.AppendFormatted(groundType, 0, null, "groundType");
				message.AppendLiteral("]的配置");
			}
			MiniMapSettingsDiagnostic.Error(ref message, null, "GetTile", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/Console/Config/MiniMapSettings.cs", 139);
		}
		return val2;
		static Tile GetTile(Dictionary<string, Dictionary<GroundType, Tile>> areaTagDict, string key, GroundType key2)
		{
			if (!areaTagDict.TryGetValue(key, out var value2))
			{
				return null;
			}
			value2.TryGetValue(key2, out var value3);
			return value3;
		}
	}

	private void RefreshTileDict()
	{
		mTileSettingDict = new Dictionary<int, Dictionary<string, Dictionary<GroundType, Tile>>>();
		foreach (MiniMapTileSetting tileSetting in TileSettings)
		{
			Dictionary<string, Dictionary<GroundType, Tile>> dictionary = new Dictionary<string, Dictionary<GroundType, Tile>>();
			MiniMapSettingsErrorHandler message;
			bool enabled;
			foreach (MiniMapTileSetting.AreaTagTileData areaTileData in tileSetting.AreaTileDatas)
			{
				string text = areaTileData.AreaTag;
				if (string.IsNullOrEmpty(text))
				{
					text = "IgnoreKey";
				}
				if (dictionary.TryGetValue(text, out var value))
				{
					message = new MiniMapSettingsErrorHandler(49, 2, out enabled);
					if (enabled)
					{
						message.AppendLiteral("MiniMapSettings这个SO资源的地图[");
						message.AppendFormatted(("[" + string.Join(",", tileSetting.MapClassIds) + "]"), 0, null, "JsonUtils.ToJson(setting.MapClassIds)");
						message.AppendLiteral("]有重复的AreaTag配置，AreaTag:");
						message.AppendFormatted(areaTileData.AreaTag, 0, null, "areaTagData.AreaTag");
						message.AppendLiteral(".");
					}
					MiniMapSettingsDiagnostic.Error(ref message, null, "RefreshTileDict", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/Console/Config/MiniMapSettings.cs", 176);
					continue;
				}
				value = (dictionary[text] = new Dictionary<GroundType, Tile>());
				foreach (MiniMapTileSetting.GroundTypeTileData groundTypeTileData in areaTileData.GroundTypeTileDatas)
				{
					if (!value.TryAdd(groundTypeTileData.GroundType, groundTypeTileData.Tile))
					{
						message = new MiniMapSettingsErrorHandler(56, 3, out enabled);
						if (enabled)
						{
							message.AppendLiteral("MiniMapSettings这个SO资源的地图[");
							message.AppendFormatted(("[" + string.Join(",", tileSetting.MapClassIds) + "]"), 0, null, "JsonUtils.ToJson(setting.MapClassIds)");
							message.AppendLiteral("]的AreaTag为[");
							message.AppendFormatted(areaTileData.AreaTag, 0, null, "areaTagData.AreaTag");
							message.AppendLiteral("]的配置中，存在多个对同一地形[");
							message.AppendFormatted(groundTypeTileData.GroundType, 0, null, "data.GroundType");
							message.AppendLiteral("]的配置");
						}
						MiniMapSettingsDiagnostic.Error(ref message, null, "RefreshTileDict", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/Console/Config/MiniMapSettings.cs", 184);
					}
				}
			}
			foreach (int mapClassId in tileSetting.MapClassIds)
			{
				if (!mTileSettingDict.TryAdd(mapClassId, dictionary))
				{
					message = new MiniMapSettingsErrorHandler(31, 1, out enabled);
					if (enabled)
					{
						message.AppendLiteral("MiniMapSettings这个SO资源的地图[");
						message.AppendFormatted(mapClassId, 0, null, "mapClassId");
						message.AppendLiteral("]有重复配置");
					}
					MiniMapSettingsDiagnostic.Error(ref message, null, "RefreshTileDict", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/Console/Config/MiniMapSettings.cs", 194);
				}
			}
		}
	}
}

}
