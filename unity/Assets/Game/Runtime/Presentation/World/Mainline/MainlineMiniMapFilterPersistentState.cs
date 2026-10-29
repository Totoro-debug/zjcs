using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zjcs.Offline.State;
using Zjcs.Official.World.Map13MiniMap;

namespace Zjcs.Presentation.World
{
    internal sealed class MainlineMiniMapFilterPersistentState : IMiniMapFilterPersistentState
    {
        [Serializable]
        private sealed class Payload
        {
            public int schema = 1;
            public int selection;
            public Entry[] filters = Array.Empty<Entry>();
        }

        [Serializable]
        private sealed class Entry
        {
            public string name;
            public bool selected;
        }

        private readonly string path;

        public MiniMapMarkUIFilterSetting State { get; }

        internal MainlineMiniMapFilterPersistentState(ProfileSlotCatalog slots, string slotId)
            : this(Path.Combine(slots.GetSlotDirectory(slotId), "minimap-filter.json"))
        {
        }

        internal MainlineMiniMapFilterPersistentState(string path)
        {
            this.path = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
            State = ReadState();
        }

        public UniTask WriteStateAsync()
        {
            var payload = new Payload
            {
                selection = (int)State.SelectionState,
                filters = State.FilterState.OrderBy(value => value.Key, StringComparer.Ordinal)
                    .Select(value => new Entry { name = value.Key, selected = value.Value }).ToArray()
            };
            LocalJsonSaveFile.Write(path, JsonUtility.ToJson(payload, true), IsValid);
            return UniTask.CompletedTask;
        }

        private MiniMapMarkUIFilterSetting ReadState()
        {
            SaveReadResult saved = LocalJsonSaveFile.Read(path, IsValid);
            if (saved.Status == SaveReadStatus.Corrupt)
                throw new InvalidDataException("小地图筛选存档损坏。");

            var state = new MiniMapMarkUIFilterSetting();
            if (saved.Json == null) return state;
            Payload payload = JsonUtility.FromJson<Payload>(saved.Json);
            state.SelectionState = (MiniMapMarkUIFilterSetting.SelectionType)payload.selection;
            foreach (Entry row in payload.filters) state.FilterState.Add(row.name, row.selected);
            return state;
        }

        private static bool IsValid(string json)
        {
            try
            {
                Payload payload = JsonUtility.FromJson<Payload>(json);
                return payload != null && payload.schema == 1 && payload.filters != null
                    && Enum.IsDefined(typeof(MiniMapMarkUIFilterSetting.SelectionType), payload.selection)
                    && payload.filters.Length <= 4096
                    && payload.filters.All(row => row != null && !string.IsNullOrWhiteSpace(row.name) && row.name.Length <= 256)
                    && payload.filters.Select(row => row.name).Distinct(StringComparer.Ordinal).Count() == payload.filters.Length;
            }
            catch
            {
                return false;
            }
        }
    }
}
