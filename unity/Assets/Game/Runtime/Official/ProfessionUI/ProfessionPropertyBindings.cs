using System;
using UnityEngine;
using Zjcs.Official.World.NativeLabelUI.Framework;

namespace Zjcs.Official.ProfessionUI
{
    public sealed class ProfessionPropertyDefinition
    {
        public bool Percent, Float;
        public int EntryId;
        public string Unit, SmallIcon;
    }

    public interface IProfessionPropertyBindings
    {
        LanguageType Language { get; }
        IProfessionImageResourceLoader Resources { get; }
        ProfessionPropertyDefinition GetProperty(ProfessionPropertyType type);
        string GetEntryIcon(int entryId);
        string Text(string key, params object[] args);
        string TextFormatted(string key, params object[] args);
        object DecimalNumber(long value, int decimalPlaces);
        void PopupEntryDetail(IWindowOperator owner, string spritePath, ProfessionItemText name, ProfessionItemText description, bool controlXPos);
        void PopupBless(IWindowOperator owner, int itemRank, string description, bool isEffect, string unEffectKey, string unSeasonMatchKey);
    }

    public static class ProfessionPropertyBindings
    {
        public static IProfessionPropertyBindings Current { get; set; }
        public static IProfessionPropertyBindings Required => Current ??
            throw new InvalidOperationException("Original property presentation services are unbound.");
    }
}
