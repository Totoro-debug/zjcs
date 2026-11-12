using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;

namespace Zjcs.Offline.HomeHudUI
{
    // Supplies the original fields omitted from the older transaction catalog.
    // The latter keeps its own source identity and remains the claim authority.
    public sealed class OfflineHomeMainlineTaskData
    {
        public sealed class TaskConfig
        {
            readonly JObject original;
            internal TaskConfig(JObject row) { original = row; }
            public int ClassId => (int)original["ClassId"];
            public int Order => (int)original["Order"];
            public bool IsAllowTrace => !(bool)original["BanTrace"];
            public bool AutoTracing => (bool)original["AutoTracing"];
            public bool ShowProgressBar => (bool)original["ShowProgressBar"];
            public bool? FinishJumpable => (bool?)original["FinishCondition"]["Jumpable"];
            // Selected TaskInfoParser.PostParse and TaskInfoUtils.GetTaskTitleKey.
            public string TitleKey => $"task_{ClassId}_title";
            public string ProgressKey => $"task_{ClassId}_progress";
            public JObject CopyOriginalRow() => (JObject)original.DeepClone();
        }

        readonly Dictionary<int, TaskConfig> tasks;
        readonly Dictionary<int, JObject> items;
        readonly JObject taskSettings;
        public IReadOnlyList<TaskConfig> OrderedTasks { get; }
        public IReadOnlyDictionary<string, string> Texts { get; }

        OfflineHomeMainlineTaskData(JObject data)
        {
            tasks = ((JArray)data["tasks"]).Cast<JObject>().Select(row => new TaskConfig(row))
                .ToDictionary(row => row.ClassId);
            if (tasks.Count != 55) throw new FormatException("Selected mainline task scope is incomplete.");
            OrderedTasks = Array.AsReadOnly(tasks.Values.OrderBy(row => row.Order).ThenBy(row => row.ClassId).ToArray());
            items = ((JArray)data["items"]).Cast<JObject>().ToDictionary(row => (int)row["ClassId"]);
            taskSettings = (JObject)data["taskSettings"];
            Texts = new ReadOnlyDictionary<string, string>(((JArray)data["texts"])
                .ToDictionary(row => (string)row["key"], row => (string)row["text"]));
            foreach (var task in tasks.Values)
                if (!Texts.ContainsKey(task.TitleKey) || !Texts.ContainsKey(task.ProgressKey))
                    throw new FormatException("Original dynamic task text is missing: " + task.ClassId);
        }

        public static OfflineHomeMainlineTaskData LoadDefault()
        {
            var data = Resources.Load<TextAsset>("Home/home-mainline-task-data");
            var proof = Resources.Load<TextAsset>("Home/home-mainline-task-data-hash");
            var transactions = Resources.Load<TextAsset>(OfficialMainlineTaskCatalog.DefaultResourcePath);
            if (!data || !proof || !transactions)
                throw new InvalidOperationException("Selected original home task data is unbound.");
            return Load(data.text, proof.text, transactions.text);
        }

        public static OfflineHomeMainlineTaskData Load(string json, string proofJson, string transactionsJson)
        {
            var proof = JObject.Parse(proofJson);
            if ((string)proof["manifest"] != "884_156692" || (string)proof["output_sha256"] != Hash(json))
                throw new FormatException("Selected home task payload differs from its source hash.");
            var data = JObject.Parse(json);
            if ((string)data["manifest"] != "884_156692" || (string)data["transactionCatalogSha256"] != Hash(transactionsJson))
                throw new FormatException("Home task data requires the exactly compared transaction catalog.");
            return new OfflineHomeMainlineTaskData(data);
        }

        static string Hash(string value)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value)))
                    .Replace("-", "").ToLowerInvariant();
        }

        public TaskConfig Task(int classId) => tasks[classId];
        public JObject CopyItemRow(int classId) => (JObject)items[classId].DeepClone();
        public JObject CopyTaskSettings() => (JObject)taskSettings.DeepClone();
        public string Text(string key) => Texts.TryGetValue(key, out string value) ? value : key;
    }
}
