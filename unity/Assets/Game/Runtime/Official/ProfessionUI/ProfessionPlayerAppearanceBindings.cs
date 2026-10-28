using System;
using System.Collections.Generic;

namespace Zjcs.Official.ProfessionUI
{
    // Protocol objects retain their identity across the original application
    // methods. The local player/data providers must be bound explicitly.
    public interface IProfessionPlayerAppearanceSource
    {
        long Id { get; }
        Sex Sex { get; }
        int Profession { get; }
        object AppearanceData { get; }
        event Action<AppearanceOperateSource> OnAppearanceUpdate;
    }
    public sealed class ProfessionPlayerLiteAppearance
    {
        public Sex Sex;
        public int Profession;
        public object AppearanceData;
    }
    public interface IProfessionPlayerAppearanceBindings
    {
        IProfessionPlayerAppearanceSource MainPlayer { get; }
        AppearanceConfig Config { get; }
        AppearanceAnimNameCombineRule AnimationRules { get; }
        bool IsAppearanceValid(object data);
        IEnumerable<string> GetSkinNames(object data);
        void GetLiteInfo(long playerId, ProfessionLiteInfoCallback callback, bool forceUpdateInfo);
        void BuildAppearanceRuntimeData(Sex sex, int profession, object data, List<string> appendSkins, AppearanceRuntimeData target);
        int GetBaseProfession(int profession);
        string GetProfessionIdStr(int profession);
    }
    public static class ProfessionPlayerAppearanceBindings
    {
        public static IProfessionPlayerAppearanceBindings Current { get; set; }
        public static IProfessionPlayerAppearanceBindings Required => Current ??
            throw new InvalidOperationException("Original player appearance data/events are unbound.");
    }
}
