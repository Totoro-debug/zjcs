using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Zjcs.Official.ProfessionUI
{
    public interface IProfessionPortraitAppearanceReader
    {
        long GetModifyTimeStamp(object appearance);
        IEnumerable<KeyValuePair<SkinType, object>> GetSkins(object appearance);
        int GetSkinId(object skin);
    }

    // The protocol AppearanceData object crosses this module as object. Read the
    // original public field schema without changing, cloning or serializing it.
    public sealed class ProfessionPortraitAppearanceReader : IProfessionPortraitAppearanceReader
    {
        public long GetModifyTimeStamp(object appearance)
            => (long)Field(appearance, "ModifyTimeStamp").GetValue(appearance);

        public IEnumerable<KeyValuePair<SkinType, object>> GetSkins(object appearance)
        {
            var skins = (IDictionary)Field(appearance, "SkinMap").GetValue(appearance);
            foreach (DictionaryEntry skin in skins)
                yield return new KeyValuePair<SkinType, object>((SkinType)Convert.ToInt32(skin.Key), skin.Value);
        }

        public int GetSkinId(object skin) => (int)Field(skin, "SkinId").GetValue(skin);

        private static FieldInfo Field(object value, string name)
            => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public)
                ?? throw new MissingFieldException(value.GetType().FullName, name);
    }
}
