using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    // AppearanceDataEx.SetInitialWeaponSkin/SetProfessionDefaultSkin/SetSkinById
    // with the original Player/global parser access supplied explicitly.
    public sealed class ProfessionAppearanceSkinDefaults
    {
        private readonly ProfessionAppearanceTables tables;
        private readonly Settings settings;
        private readonly Dictionary<Profession, List<EquipPos>> positions = new Dictionary<Profession, List<EquipPos>>();
        private readonly Action<string> error;

        public ProfessionAppearanceSkinDefaults(ProfessionAppearanceTables tables, string json, Action<string> error)
        {
            this.tables = tables ?? throw new ArgumentNullException(nameof(tables));
            this.error = error ?? throw new ArgumentNullException(nameof(error));
            settings = JsonUtility.FromJson<Settings>(json ?? throw new ArgumentNullException(nameof(json)))
                ?? throw new ArgumentException("Missing original appearance default settings.", nameof(json));
            // EquipTypeInfoParser.PostParse: retain original row order and repeated positions.
            foreach (var row in settings.EquipTypes)
                foreach (var profession in row.Professions)
                {
                    if (!positions.TryGetValue(profession, out var list))
                    {
                        list = new List<EquipPos>(); positions.Add(profession, list);
                    }
                    list.Add(row.EquipPos);
                }
        }

        public IReadOnlyList<EquipPos> GetEquipPosByProfession(Profession profession)
        {
            if (!positions.TryGetValue(profession, out var list))
                error("GetEquipPosByProfession failed, Profession:" + profession);
            return list;
        }

        public void SetInitialWeaponSkin(Sex sex, Profession profession, AppearanceData data, int weaponType,
            Action<AppearanceOperateSource> dirty)
        {
            if (!data.SkinMap.ContainsKey(SkinType.pWeapon))
            {
                int id = weaponType == 200 ? settings.InitialWeaponSwordSkinId : settings.InitialWeaponWandSkinId;
                SetSkinById(sex, profession, data, SkinType.pWeapon, id);
                dirty(AppearanceOperateSource.Normal);
            }
        }

        public void SetProfessionDefaultSkin(Sex sex, Profession currentProfession, AppearanceData data,
            Profession profession, AppearanceOperateSource source, Action<AppearanceOperateSource> dirty)
        {
            if (!tables.ProfessionBaseInfoParser.Get(profession).DefaultSkins.TryGetValue(sex, out var skins))
            {
                error("proBaseInfo.DefaultSkins is not exit " + sex); return;
            }
            var equipment = GetEquipPosByProfession(profession);
            foreach (var type in settings.ProfessionSkinTypeList)
            {
                if (!skins.TryGetValue(type, out var id) || !Contains(equipment, type.ToEquipPos()))
                    data.SkinMap.Remove(type);
                else SetSkinById(sex, currentProfession, data, type, id);
            }
            dirty(source);
        }

        public void SetSkinById(Sex sex, Profession profession, AppearanceData data, SkinType type, int id)
        {
            if (!tables.CharacterSkinInfoParser.GetSkinInfo(sex, profession).ContainsSkin(type, id))
                error("skin id is not exist:" + id);
            else
            {
                if (!data.SkinMap.TryGetValue(type, out var skin))
                {
                    skin = new SkinData { Type = type }; data.SkinMap.Add(type, skin);
                }
                skin.SkinId = id;
            }
        }

        private static bool Contains(IReadOnlyList<EquipPos> positions, EquipPos target)
        {
            for (int i = 0; i < positions.Count; i++) if (positions[i] == target) return true;
            return false;
        }

        [Serializable] private sealed class Settings
        {
            public int InitialWeaponSwordSkinId, InitialWeaponWandSkinId;
            public SkinType[] ProfessionSkinTypeList;
            public EquipType[] EquipTypes;
        }
        [Serializable] private sealed class EquipType
        {
            public EquipPos EquipPos;
            public Profession[] Professions;
        }
    }
}
