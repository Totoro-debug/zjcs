using System;

namespace Zjcs.Official.World
{
    [Serializable]
    public sealed class OfficialMapDefinition
    {
        public int ClassId;
        public int MapGroupId;
        public int MapTagId;
        public int MapBgmId;
        public int NextMapClassId;
        public string OriginalGroundPrefab;
        public string OriginalMiniMapPrefab;
        public string SourceManifest;
        public string SourceManifestSha256;
        public OfficialMapCharacterMover CharacterMover;
        public OfficialMapRegionProgress[] MaxRegionProgress;
        public OfficialMapNavigation Navigation;
        public OfficialMapEntityPlacement[] Entities;
        public OfficialMapEntityComponents[] EntityComponents;
        public OfficialWorldConfigRecord[] Configs;
    }

    [Serializable]
    public sealed class OfficialMapRegionProgress
    {
        public int Region;
        public int Value;
    }

    [Serializable]
    public sealed class OfficialMapCharacterMover
    {
        public int CharacterClassId;
        public int MoveActionId;
        public int WalkableGroundTypeKey;
        public int WalkableGroundTypeServerOverrideKey;
        public int[] WalkableGroundTypes;
        public int[] ServerOverrideGroundTypes;
        public int MoveMask;
    }

    [Serializable]
    public sealed class OfficialMapSourceManifest
    {
        public int format;
        public int mapClassId;
        public string sourceInputsJson;
        public string sourceInputsSha256;
        public OfficialMapSourceOutput output;
    }

    [Serializable]
    public sealed class OfficialMapSourceOutput
    {
        public string path;
        public int size;
        public string sha256;
    }

    [Serializable]
    public sealed class OfficialMapNavigation
    {
        public int Width;
        public int Height;
        public OfficialMapVector3 Origin;
        public int CharacterId;
        public int MemberId;
        public OfficialMapGridRow[] Rows;
        public OfficialMapAreaMapping[] AreaMappings;
        public int MaxSteps;
    }

    [Serializable]
    public sealed class OfficialMapGridRow
    {
        public OfficialMapGridCell[] Cells;
    }

    [Serializable]
    public sealed class OfficialMapGridCell
    {
        public int[] RawValues;
    }

    [Serializable]
    public sealed class OfficialMapAreaMapping
    {
        public int[] Cell;
        public int Value;
    }

    [Serializable]
    public sealed class OfficialMapEntityPlacement
    {
        public int Id;
        public int Region;
        public int Refresh;
        public bool[] Public;
        public bool[] Private;
        public int[] Grid;
        public int Direction;
        public bool Unique;
        public int Area;
        public int EntityId;
        public int TypeId;
        public OfficialMapStageValue[] Stages;
    }

    [Serializable]
    public sealed class OfficialMapStageValue
    {
        public string Key;
        public string ValueJson;
    }

    [Serializable]
    public sealed class OfficialMapEntityComponents
    {
        public int EntityId;
        public string Json;
    }

    [Serializable]
    public sealed class OfficialWorldConfigRecord
    {
        public string TableName;
        public string RecordId;
        public string Json;
        public string Sha256;
        public string SourceBundle;
        public string SourcePathId;
    }

    [Serializable]
    public struct OfficialMapVector3
    {
        public float X;
        public float Y;
        public float Z;
    }
}
