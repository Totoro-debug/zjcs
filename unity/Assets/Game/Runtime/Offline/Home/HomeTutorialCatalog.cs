using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.Home
{
    [Serializable]
    public sealed class HomeTutorialConnection
    {
        public int node;
        public string port;
    }

    [Serializable]
    public sealed class HomeTutorialPort
    {
        public string name;
        public HomeTutorialConnection[] connections;
    }

    [Serializable]
    public sealed class HomeTutorialNode
    {
        public int id;
        public string type;
        public string name;
        public string payload;
        public HomeTutorialPort[] ports;
    }

    [Serializable]
    public sealed class HomeTutorialDialogue
    {
        public int nodeId;
        public int order;
        public string text;
        public int roleId;
        public string face;
        public string type;
        public string voiceEvent;
    }

    [Serializable]
    public sealed class HomeTutorialStoryOption
    {
        public int id;
        public string text;
        public string icon;
    }

    [Serializable]
    public sealed class HomeTutorialGuide
    {
        public string name;
        public int order;
        public string resPath;
        public HomeTutorialNode[] nodes;
        public HomeTutorialDialogue[] dialogues;
        public string graphPropertiesJson;
    }

    [Serializable]
    public sealed class HomeTutorialData
    {
        public string version;
        public HomeTutorialGuide[] guides;
        public string sourceSha256;
    }

    /// <summary>
    /// Fixed-version Home tutorial graphs/dialogues decoded from Adventure tables.
    /// This catalog retains every official node, port connection and dialogue line;
    /// runtime graph execution can therefore be implemented without inventing steps.
    /// </summary>
    public sealed class HomeTutorialCatalog
    {
        // Decoded from story_option_dialog.g.bytes for manifest 854_156383.
        private static readonly HomeTutorialStoryOption[] OfficialStoryOptions =
        {
            new HomeTutorialStoryOption
            {
                id = 975,
                text = "接下来做什么？",
                icon = "UI/Dialogue/Iconarrow_1"
            },
            new HomeTutorialStoryOption
            {
                id = 980,
                text = "我要拯救世界……",
                icon = "UI/Dialogue/Iconarrow_1"
            }
        };

        private static readonly string[] RequiredGuides =
        {
            "RepaireFurniture2", "GoHomeUnlockHeap", "RepaireBed",
            "BedSpeedUp", "RepaireBaGuaLu", "FirstMergeItem"
        };

        private readonly Dictionary<string, HomeTutorialGuide> byName;
        public HomeTutorialData Data { get; }
        public IReadOnlyList<HomeTutorialGuide> Guides => Data.guides;
        public int TotalNodeCount => (Data.guides ?? Array.Empty<HomeTutorialGuide>())
            .Sum(value => value.nodes == null ? 0 : value.nodes.Length);

        private HomeTutorialCatalog(HomeTutorialData data)
        {
            Data = data;
            byName = data.guides.ToDictionary(value => value.name, StringComparer.Ordinal);
        }

        public static HomeTutorialCatalog LoadDefault()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Home", "tutorials.json");
            if (!File.Exists(path)) throw new FileNotFoundException("Official home tutorial graph payload is missing.", path);
            HomeTutorialData data = JsonUtility.FromJson<HomeTutorialData>(File.ReadAllText(path));
            if (data == null || data.version != "854_156383" || data.guides == null
                || data.guides.Length != RequiredGuides.Length || string.IsNullOrEmpty(data.sourceSha256))
                throw new FormatException("Official home tutorial payload is incomplete or from an unsupported version.");
            foreach (string name in RequiredGuides)
            {
                HomeTutorialGuide guide = data.guides.FirstOrDefault(value => value.name == name);
                if (guide == null || guide.nodes == null || guide.nodes.Length == 0 || guide.dialogues == null)
                    throw new FormatException("Official home tutorial graph is incomplete: " + name);
                var ids = new HashSet<int>();
                foreach (HomeTutorialNode node in guide.nodes)
                    if (node == null || !ids.Add(node.id) || string.IsNullOrEmpty(node.type))
                        throw new FormatException("Invalid tutorial node in " + name);
                foreach (HomeTutorialNode node in guide.nodes)
                    foreach (HomeTutorialConnection connection in (node.ports ?? Array.Empty<HomeTutorialPort>())
                                 .Where(value => value != null).SelectMany(value => value.connections ?? Array.Empty<HomeTutorialConnection>()))
                        if (!ids.Contains(connection.node))
                            throw new FormatException("Tutorial connection points outside graph " + name + ": " + connection.node);
            }
            return new HomeTutorialCatalog(data);
        }

        public HomeTutorialGuide Guide(string name) =>
            name != null && byName.TryGetValue(name, out HomeTutorialGuide guide) ? guide : null;

        public IReadOnlyList<HomeTutorialDialogue> Dialogues(string guideName, int nodeId) =>
            Guide(guideName)?.dialogues.Where(value => value.nodeId == nodeId)
                .OrderBy(value => value.order).ToArray() ?? Array.Empty<HomeTutorialDialogue>();

        public HomeTutorialStoryOption StoryOption(string guideName, int optionId) =>
            guideName == "RepaireFurniture2"
                ? OfficialStoryOptions.FirstOrDefault(value => value.id == optionId) : null;

        public HomeTutorialConnection FirstConnection(HomeTutorialNode node, string portName)
        {
            HomeTutorialPort port = (node?.ports ?? Array.Empty<HomeTutorialPort>())
                .FirstOrDefault(value => value != null && value.name == portName);
            return port?.connections?.FirstOrDefault();
        }
    }
}
