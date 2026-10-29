using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;

namespace Zjcs.Offline.ProfessionUI
{
    // A local, explicitly supplied binding for the consumed original tables
    // and language service. Window creation remains owned by the UI host.
    public sealed class OfflineProfessionPropertyBindings : IProfessionPropertyBindings
    {
        [Serializable] private sealed class Tables { public Property[] prop_cfg; public Entry[] entry; }
        [Serializable] private sealed class Property
        {
            public int PropType, EntryId;
            public bool Percent, Float;
            public string Unit, SmallIcon;
        }
        [Serializable] private sealed class Entry { public int Id; public string Icon; }
        private readonly Dictionary<ProfessionPropertyType, ProfessionPropertyDefinition> properties = new Dictionary<ProfessionPropertyType, ProfessionPropertyDefinition>();
        private readonly Dictionary<int, string> entryIcons = new Dictionary<int, string>();
        private readonly ILocalizationTextService language;
        private readonly Action<IWindowOperator, string, ProfessionItemText, ProfessionItemText, bool> popupEntry;
        private readonly Action<IWindowOperator, int, string, bool, string, string> popupBless;
        public LanguageType Language => language.CurrentLanguage;
        public IProfessionImageResourceLoader Resources { get; }

        public OfflineProfessionPropertyBindings(TextAsset config, IProfessionImageResourceLoader resources, ILocalizationTextService localization,
            Action<IWindowOperator, string, ProfessionItemText, ProfessionItemText, bool> entryPopup,
            Action<IWindowOperator, int, string, bool, string, string> blessingPopup)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            Resources = resources ?? throw new ArgumentNullException(nameof(resources));
            language = localization ?? throw new ArgumentNullException(nameof(localization));
            popupEntry = entryPopup ?? throw new ArgumentNullException(nameof(entryPopup));
            popupBless = blessingPopup ?? throw new ArgumentNullException(nameof(blessingPopup));
            var tables = JsonUtility.FromJson<Tables>(config.text);
            foreach (var row in tables.prop_cfg)
                properties.Add((ProfessionPropertyType)row.PropType, new ProfessionPropertyDefinition { Percent = row.Percent, Float = row.Float, EntryId = row.EntryId, Unit = row.Unit, SmallIcon = row.SmallIcon });
            foreach (var row in tables.entry) entryIcons.Add(row.Id, row.Icon);
        }

        public ProfessionPropertyDefinition GetProperty(ProfessionPropertyType type) => properties[type];
        public string GetEntryIcon(int entryId) => entryIcons[entryId];
        public string Text(string key, params object[] args) => language.GetText(key, Language, args);
        public string TextFormatted(string key, params object[] args) => language.GetTextFormated(key, Language, args);
        public object DecimalNumber(long value, int decimalPlaces) => CultureNumberArg.Decimal(value, decimalPlaces);
        public void PopupEntryDetail(IWindowOperator owner, string path, ProfessionItemText name, ProfessionItemText description, bool controlXPos) => popupEntry(owner, path, name, description, controlXPos);
        public void PopupBless(IWindowOperator owner, int rank, string description, bool effect, string unEffectKey, string unSeasonMatchKey) => popupBless(owner, rank, description, effect, unEffectKey, unSeasonMatchKey);
    }
}
