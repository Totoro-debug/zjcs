using System;
using System.Collections.Generic;
using Zjcs.Offline.Progression;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // A read-only projection of the selected local profile. Refresh is called by
    // the owning window/session after a successful local appearance transaction.
    public sealed class OfflineProfessionAppearanceBindings : IProfessionPlayerAppearanceBindings,
        IProfessionAppearancePreviewBindings, IProfessionPlayerAppearanceSource
    {
        private readonly Func<OfflinePlayerProgress> readPlayer;
        private readonly Func<int, int?> itemType;
        private readonly ProfessionAppearanceTables tables;
        private readonly ProfessionAppearanceResolver resolver;
        private AppearanceData appearance;
        private int initialWeaponType;
        private int? equippedWeaponType;
        public long Id { get; }
        public Sex Sex { get; private set; }
        public int Profession { get; private set; }
        public object AppearanceData => appearance;
        public event Action<AppearanceOperateSource> OnAppearanceUpdate;
        public IProfessionPlayerAppearanceSource MainPlayer => this;
        public IProfessionPlayerAppearanceBindings Player => this;
        public AppearanceConfig Config { get; }
        public AppearanceAnimNameCombineRule AnimationRules { get; }

        public OfflineProfessionAppearanceBindings(long playerId, Func<OfflinePlayerProgress> readPlayer,
            Func<int, int?> itemType, ProfessionAppearanceTables tables, AppearanceConfig config,
            AppearanceAnimNameCombineRule animationRules)
        {
            if (playerId <= 0) throw new ArgumentOutOfRangeException(nameof(playerId));
            Id = playerId;
            this.readPlayer = readPlayer ?? throw new ArgumentNullException(nameof(readPlayer));
            this.itemType = itemType ?? throw new ArgumentNullException(nameof(itemType));
            this.tables = tables ?? throw new ArgumentNullException(nameof(tables));
            Config = config ? config : throw new ArgumentNullException(nameof(config));
            AnimationRules = animationRules ? animationRules : throw new ArgumentNullException(nameof(animationRules));
            resolver = new ProfessionAppearanceResolver(tables);
            Refresh(AppearanceOperateSource.Normal, false);
        }

        public void Refresh(AppearanceOperateSource source, bool notify = true)
        {
            var player = readPlayer() ?? throw new InvalidOperationException("Selected local player is unavailable.");
            var wardrobe = player.Wardrobe ?? throw new InvalidOperationException("Selected wardrobe is unavailable.");
            // Construct everything first: a missing current item row must not
            // publish half of a new appearance to the original event listeners.
            int? weapon = ReadEquippedWeaponType(player);
            long revision = Math.Max(DateTime.UtcNow.Ticks, checked((appearance?.ModifyTimeStamp ?? 0) + 1));
            var snapshot = ReadAppearance(wardrobe, revision);
            Sex = (Sex)wardrobe.Sex;
            Profession = wardrobe.Profession;
            initialWeaponType = player.InitialWeaponType;
            equippedWeaponType = weapon;
            appearance = snapshot;
            if (notify) OnAppearanceUpdate?.Invoke(source);
        }

        private int? ReadEquippedWeaponType(OfflinePlayerProgress player)
        {
            foreach (var slot in player.Equipment.Equipped)
            {
                if (slot.Position != (int)EquipPos.Weapon) continue;
                foreach (var instance in player.Equipment.Instances)
                    if (instance.InstanceId == slot.InstanceId)
                        return itemType(instance.ItemId) ?? throw new InvalidOperationException(
                            "Current official item type is unbound: " + instance.ItemId);
                return 0; // Original equipped object with no GetItem result => ItemType.None.
            }
            return null; // No equipped weapon: original InitialWeaponType fallback.
        }

        public static AppearanceData ReadAppearance(OfflineWardrobeProgress wardrobe, long revision)
        {
            if (wardrobe == null) throw new ArgumentNullException(nameof(wardrobe));
            var data = new AppearanceData { ModifyTimeStamp = revision, AppearanceLevel = wardrobe.AppearanceLevel };
            foreach (var value in wardrobe.BaseSkins)
                data.SkinMap.Add((SkinType)value.Type, new SkinData { Type = (SkinType)value.Type, SkinId = value.Id });
            // The local equipment synchronizer stores SkinType, whereas the
            // official protocol uses EquipPos. Preserve absent versus explicit 0.
            foreach (var value in wardrobe.WornSkins)
            {
                var type = (SkinType)value.Type;
                if (type != SkinType.body && type != SkinType.pWeapon && type != SkinType.sWeapon)
                    throw new InvalidOperationException("Unsupported equipped appearance type: " + value.Type);
                data.EquipSkinDict.Add(type.ToEquipPos(), value.Id);
            }
            foreach (var value in wardrobe.WornItems) data.AppendAppearanceDict.Add((SkinType)value.Type, value.Id);
            foreach (var value in wardrobe.WearTimes)
                data.AppendAppearanceWearTimeDict.Add((SkinType)value.Type,
                    DateTimeOffset.FromUnixTimeSeconds(value.UnixSeconds).UtcDateTime);
            foreach (var value in wardrobe.Dyes)
                data.DyeMap.Add((DyeType)value.Type, new SkinDyeData
                { ColorId = value.ColorId, Hsv = new Hsv(value.Hue, value.Saturation, value.Value) });
            if (wardrobe.WornTitleId != 0) data.HonorItemDict.Add(HonorType.Title, wardrobe.WornTitleId);
            // FullBodyAppearanceId and LimitItemExpSet have no counterparts in
            // this save schema and are not consumed by the scoped resolver.
            return data;
        }

        public bool IsAppearanceValid(object data) => resolver.IsAppearanceDataValid(RequireData(data), Config);
        public IEnumerable<string> GetSkinNames(object data)
        {
            foreach (var skin in RequireData(data).SkinMap) yield return resolver.GetSkinName(skin.Value.SkinId);
        }
        public void GetLiteInfo(long playerId, ProfessionLiteInfoCallback callback, bool forceUpdateInfo)
        {
            if (playerId != Id) throw new InvalidOperationException("Only the selected local player's appearance is bound.");
            if (forceUpdateInfo) Refresh(AppearanceOperateSource.Normal, false);
            callback?.Invoke(new ProfessionPlayerLiteAppearance { Sex = Sex, Profession = Profession, AppearanceData = appearance });
        }
        public void BuildAppearanceRuntimeData(Sex sex, int profession, object data, List<string> appendSkins, AppearanceRuntimeData target)
            => resolver.BuildAppearanceRuntimeData(sex, (Profession)profession, RequireData(data), appendSkins, target);

        // Original AppearanceUtils.GetBaseProfession/GetProfessionIdStr and
        // ProfessionBaseInfoParser.GetProfessionsSerial, with explicit context.
        public int GetBaseProfession(int profession)
        {
            if (profession == 0)
            {
                int type = equippedWeaponType ?? initialWeaponType;
                return type != 200 && type != 0 ? (int)Official.ProfessionUI.Profession.Fashi : (int)Official.ProfessionUI.Profession.Zhanshi;
            }
            var info = tables.ProfessionBaseInfoParser.Get(profession);
            while (info.PrePro != Official.ProfessionUI.Profession.None) info = tables.ProfessionBaseInfoParser.Get(info.PrePro);
            return (int)info.Profession;
        }
        public string GetProfessionIdStr(int profession)
        {
            int baseProfession = GetBaseProfession(profession);
            if (baseProfession == (int)Official.ProfessionUI.Profession.Zhanshi) return "100";
            if (baseProfession == (int)Official.ProfessionUI.Profession.Fashi) return "200";
            return string.Empty;
        }
        public ProfessionAppearancePreviewDefinition GetProfession(int profession)
        {
            var info = tables.ProfessionBaseInfoParser.Get(profession);
            return new ProfessionAppearancePreviewDefinition { DefaultSkins = info.DefaultSkins, AppendAppearanceRewards = info.AppendAppearanceRewards };
        }
        public ProfessionAppearancePreviewData CloneAppearance(object data)
        {
            var clone = Clone(RequireData(data));
            return new ProfessionAppearancePreviewData(clone, clone.AppendAppearanceDict, clone.EquipSkinDict);
        }

        // Independent offline equivalent of the original serialization clone.
        // Every protocol field, null value and dictionary iteration order survives.
        public static AppearanceData Clone(AppearanceData source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new AppearanceData
            {
                SkinMap = Copy(source.SkinMap, v => v == null ? null : new SkinData { Type = v.Type, SkinId = v.SkinId }),
                DyeMap = Copy(source.DyeMap, v => v == null ? null : new SkinDyeData { ColorId = v.ColorId, Hsv = v.Hsv }),
                AppendAppearanceDict = Copy(source.AppendAppearanceDict, v => v),
                EquipSkinDict = Copy(source.EquipSkinDict, v => v),
                HonorItemDict = Copy(source.HonorItemDict, v => v),
                ModifyTimeStamp = source.ModifyTimeStamp,
                AppearanceLevel = source.AppearanceLevel,
                FullBodyAppearanceId = source.FullBodyAppearanceId,
                LimitItemExpSet = source.LimitItemExpSet == null ? null : new HashSet<int>(source.LimitItemExpSet),
                AppendAppearanceWearTimeDict = Copy(source.AppendAppearanceWearTimeDict, v => v)
            };
        }
        private static Dictionary<K,V> Copy<K,V>(Dictionary<K,V> source, Func<V,V> clone)
        {
            if (source == null) return null;
            var result = new Dictionary<K,V>(source.Count, source.Comparer);
            foreach (var pair in source) result.Add(pair.Key, clone(pair.Value));
            return result;
        }
        private static AppearanceData RequireData(object data) => data as AppearanceData
            ?? throw new ArgumentException("Expected the migrated official AppearanceData.", nameof(data));
    }
}
