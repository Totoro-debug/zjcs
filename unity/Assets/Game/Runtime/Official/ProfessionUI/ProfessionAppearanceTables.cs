using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    // The byte decoder verifies the selected official tables before this ordered
    // projection is generated. No reflection or network lookup is used at runtime.
    public class ProfessionAppearanceKeyTable<T> where T : class
    {
        protected readonly Dictionary<int, T> configs = new Dictionary<int, T>();
        public int Count => configs.Count;
        public T Get(int key) { configs.TryGetValue(key, out var value); return value; }
        public void Add(int key, T value) { configs.Add(key, value); }
        public T[] GetAll()
        {
            var result = new T[configs.Count]; configs.Values.CopyTo(result, 0); return result;
        }
    }

    public sealed class ProfessionAppearanceBaseInfoParser : ProfessionAppearanceKeyTable<ProfessionAppearanceBaseInfo>
    {
        public ProfessionAppearanceBaseInfo Get(Profession profession) => Get((int)profession);
    }
    public sealed class ProfessionAppearanceDyeInfoParser : ProfessionAppearanceKeyTable<CharacterSkinDyeInfo>
    {
        public CharacterSkinDyeInfo Get(DyeType dye) => Get((int)dye);
    }

    public sealed class ProfessionAppearanceTables
    {
        public readonly SpineSkinInfoParser SpineSkinInfoParser = new SpineSkinInfoParser();
        public readonly CharacterSkinIdGroupInfoParser CharacterSkinIdGroupInfoParser = new CharacterSkinIdGroupInfoParser();
        public readonly CharacterSkinInfoParser CharacterSkinInfoParser = new CharacterSkinInfoParser();
        public readonly ProfessionAppearanceBaseInfoParser ProfessionBaseInfoParser = new ProfessionAppearanceBaseInfoParser();
        public readonly ProfessionAppendAppearanceInfoParser AppendAppearanceInfoParser = new ProfessionAppendAppearanceInfoParser();
        public readonly ProfessionAppearanceDyeInfoParser CharacterSkinDyeInfoParser = new ProfessionAppearanceDyeInfoParser();
        public readonly ProfessionAppearanceKeyTable<CharacterSkinColorInfo> CharacterSkinColorInfoParser = new ProfessionAppearanceKeyTable<CharacterSkinColorInfo>();

        public static ProfessionAppearanceTables Load(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var payload = JsonUtility.FromJson<Payload>(json);
            if (payload == null) throw new ArgumentException("Appearance payload is null.", nameof(json));
            var tables = new ProfessionAppearanceTables();
            foreach (var row in payload.Spines)
            {
                // These two sets are computed by the original application parser.
                row.BlackIdSet = new HashSet<int>(); row.WhiteIdSet = new HashSet<int>();
                tables.SpineSkinInfoParser.Add(row.Id, row);
            }
            foreach (var row in payload.Groups)
            {
                var ranges = new List<int[]>();
                foreach (var range in row.Ranges) ranges.Add(range.Values);
                tables.CharacterSkinIdGroupInfoParser.Add(row.GroupId,
                    new CharacterSkinIdGroupInfo { GroupId = row.GroupId, NestedIdList = ranges });
            }
            foreach (var row in payload.Professions)
                tables.ProfessionBaseInfoParser.Add((int)row.Profession, new ProfessionAppearanceBaseInfo
                {
                    Profession = row.Profession, PrePro = row.PrePro,
                    DefaultSkins = DecodeSexSkins(row.DefaultSkins),
                    AppendAppearanceRewards = DecodeSexSkins(row.AppendAppearanceRewards)
                });
            foreach (var row in payload.Appended)
            {
                Dictionary<Sex, int> skins = null;
                if (row.Skins != null)
                {
                    skins = new Dictionary<Sex, int>();
                    foreach (var pair in row.Skins) skins.Add(pair.Sex, pair.Id);
                }
                tables.AppendAppearanceInfoParser.Add(row.ClassId,
                    new ProfessionAppendAppearanceInfo { ClassId = row.ClassId, SkinDict = skins });
            }
            foreach (var row in payload.Dyes) tables.CharacterSkinDyeInfoParser.Add((int)row.DyeType, row);
            foreach (var row in payload.Colors) tables.CharacterSkinColorInfoParser.Add(row.Id, row);
            foreach (var row in payload.Characters)
                row.TypeDic = new Dictionary<SkinType, Dictionary<SkinUseType, List<int>>>();
            tables.CharacterSkinInfoParser.Rows = payload.Characters;
            tables.SpineSkinInfoParser.PostParse();
            tables.CharacterSkinIdGroupInfoParser.PostParse();
            tables.CharacterSkinInfoParser.PostParse(tables);
            return tables;
        }

        private static Dictionary<Sex, Dictionary<SkinType, int>> DecodeSexSkins(SexSkins[] source)
        {
            if (source == null) return null;
            var result = new Dictionary<Sex, Dictionary<SkinType, int>>();
            foreach (var sex in source)
            {
                var skins = new Dictionary<SkinType, int>();
                foreach (var skin in sex.Skins) skins.Add(skin.Type, skin.Id);
                result.Add(sex.Sex, skins);
            }
            return result;
        }

        [Serializable] private sealed class Payload
        {
            public SpineSkinInfo[] Spines;
            public CharacterSkinInfo[] Characters;
            public CharacterSkinDyeInfo[] Dyes;
            public CharacterSkinColorInfo[] Colors;
            public Group[] Groups;
            public ProfessionRow[] Professions;
            public AppendRow[] Appended;
        }
        [Serializable] private sealed class IntRange { public int[] Values; }
        [Serializable] private sealed class Group { public int GroupId; public IntRange[] Ranges; }
        [Serializable] private sealed class Skin { public SkinType Type; public int Id; }
        [Serializable] private sealed class SexSkins { public Sex Sex; public Skin[] Skins; }
        [Serializable] private sealed class ProfessionRow
        {
            public Profession Profession, PrePro;
            public SexSkins[] DefaultSkins, AppendAppearanceRewards;
        }
        [Serializable] private sealed class SexSkin { public Sex Sex; public int Id; }
        [Serializable] private sealed class AppendRow { public int ClassId; public SexSkin[] Skins; }
    }
}
