using System;
using System.Collections.Generic;

namespace Zjcs.Official.ProfessionUI
{
    public sealed class ProfessionAppearancePreviewData
    {
        public readonly object Source;
        // These views must address the cloned object's actual dictionaries.
        public readonly IDictionary<SkinType,int> AppendAppearanceDict;
        public readonly IDictionary<EquipPos,int> EquipSkinDict;
        public ProfessionAppearancePreviewData(object source,IDictionary<SkinType,int> appended,IDictionary<EquipPos,int> equipment)
        {Source=source??throw new ArgumentNullException(nameof(source));AppendAppearanceDict=appended??throw new ArgumentNullException(nameof(appended));EquipSkinDict=equipment??throw new ArgumentNullException(nameof(equipment));}
    }
    public sealed class ProfessionAppearancePreviewDefinition
    {
        public Dictionary<Sex,Dictionary<SkinType,int>> DefaultSkins;
        public Dictionary<Sex,Dictionary<SkinType,int>> AppendAppearanceRewards;
    }
    public interface IProfessionAppearancePreviewBindings
    {
        IProfessionPlayerAppearanceBindings Player { get; }
        ProfessionAppearancePreviewData CloneAppearance(object data);
        ProfessionAppearancePreviewDefinition GetProfession(int profession);
    }
    public static class ProfessionAppearancePreviewBindings
    {
        public static IProfessionAppearancePreviewBindings Current { get; set; }
        public static IProfessionAppearancePreviewBindings Required => Current ??
            throw new InvalidOperationException("Original profession preview data/configuration are unbound.");
    }
}
