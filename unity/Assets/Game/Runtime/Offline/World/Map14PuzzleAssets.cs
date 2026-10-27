using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace Zjcs.Offline.World
{
    // Explicit local binding; does not register an unfinished map in the menu.
    public sealed class Map14PuzzleAssets : ScriptableObject
    {
        public TextAsset RulesJson;
        public string RulesSha256;
        [Serializable] public sealed class Item { public int ItemId, Amount; }
        [Serializable] public sealed class Cost { public int TerminalId; public Item[] Items; }
        [Serializable] public sealed class Definition
        {
            public string Manifest;
            public int MapClassId;
            public MapPuzzleRule[] Rules;
            public Cost[] Costs;
        }

        public static Map14PuzzleAssets LoadDefault()
        {
            var assets = Resources.Load<Map14PuzzleAssets>("Map14/Native/puzzle-catalog");
            if (!assets) throw new FileNotFoundException("Map14 puzzle binding is missing.");
            return assets;
        }

        public Definition Load()
        {
            if (!RulesJson) throw new InvalidDataException("Map14 puzzle data is missing.");
            using (var hash = SHA256.Create())
                if (BitConverter.ToString(hash.ComputeHash(RulesJson.bytes)).Replace("-", "").ToLowerInvariant() != RulesSha256)
                    throw new InvalidDataException("Map14 puzzle digest differs.");
            var definition = JsonUtility.FromJson<Definition>(RulesJson.text);
            if (definition == null || definition.Manifest != "871_156598" || definition.MapClassId != 14
                || definition.Rules == null || definition.Costs == null || definition.Costs.Length != 12
                || definition.Rules.Count(r => r.TerminalPuzzleType == 4) != 7
                || definition.Rules.Count(r => r.TerminalPuzzleType == 6 && r.AuxiliaryUnlockOnly) != 5)
                throw new InvalidDataException("Map14 puzzle scope differs.");
            return definition;
        }

        public OfflineMapPuzzleSession Open(MapPuzzleEntityState[] saved = null, Action<MapPuzzleTransportRequest> onTransport = null) =>
            new OfflineMapPuzzleSession(Load().Rules, saved, onTransport: onTransport);
    }
}
