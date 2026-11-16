using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Zjcs.Official.HomeStoryDialog;
using Zjcs.Official.HomeStoryUI;

namespace Zjcs.Offline.HomeStoryDialog
{
    // Local window boundary for the two recovered furniture-story dialog nodes.
    // Camera, masks and story environment remain the scene host's responsibility.
    public sealed class HomeFurnitureStoryDialogNode : IDisposable
    {
        private readonly MonoBehaviour coroutineHost;
        private readonly HomeStoryDialogWindow window;
        private Coroutine pending;
        private int generation;

        public HomeFurnitureStoryDialogNode(MonoBehaviour coroutineHost, HomeStoryDialogWindow window)
        {
            this.coroutineHost = coroutineHost ? coroutineHost : throw new ArgumentNullException(nameof(coroutineHost));
            this.window = window ?? throw new ArgumentNullException(nameof(window));
        }

        public void Begin(string story, HomeFurnitureStoryGraph.Node node, Action<int?> completed)
        {
            if (pending != null) throw new InvalidOperationException("A furniture story dialogue is already running.");
            if (node == null || completed == null) throw new ArgumentNullException();
            bool optionNode = node.Type == "OptionDialogNode";
            if (!optionNode && node.Type != "DialogNode") throw new InvalidDataException("Not an original dialogue node.");
            JObject data = node.Data;
            if (((int?)data["DialogVariantType"] ?? 0) != 0
                || ((JArray)data["DialogItems"])?.Count > 0
                || ((JArray)data["OptionParams"])?.Count > 0
                || ((JArray)data["OptionConfigs"])?.Count > 0
                || (int?)data["BlackboardRecord"]?["NeedRecord"] == 1)
                throw new InvalidDataException("The original dialogue node requires an unbound service.");
            int dialogId = (int?)data["DialogId"] ?? throw new InvalidDataException("Original dialogue ID is missing.");
            bool immediately = (int?)data["Immediately"] == 1;
            bool autoClose = (int?)data["AutoCloseDialog"] == 1;
            bool showOptions = (int?)data["ShowOptionsAtBegin"] == 1;
            int? selected = null;
            bool finished = false;
            var options = optionNode ? new List<DialogOptionDataBase>() : null;
            if (optionNode)
            {
                if (!(data["Options"] is JArray rows) || rows.Count == 0)
                    throw new InvalidDataException("Original dialogue options are missing.");
                foreach (var row in rows) options.Add(new DialogOptionData(window.Services, story, (int)row));
            }
            TalkDialog.AutoPlayConfig autoPlay = null;
            if (!optionNode && data["AutoPlayConfig"] is JObject config)
                autoPlay = new TalkDialog.AutoPlayConfig { IsAutoPlay = (int?)config["IsAutoPlay"] == 1,
                    AutoPlayWaitTime = (float?)config["AutoPlayWaitTime"] ?? 0f,
                    AutoPlayDura = (float?)config["AutoPlayDura"] ?? 0f };
            int run = ++generation;
            Action<int> onOption = optionNode ? (Action<int>)(index => selected = index) : null;
            Action<int> onFinished = optionNode ? null : (Action<int>)(id => { if (id == dialogId) finished = true; });
            window.Open(panel => panel.Init(story, dialogId, immediately, autoClose, showOptions, options,
                onOption, onFinished, null, true, autoPlay));
            pending = coroutineHost.StartCoroutine(Wait(run, () => optionNode ? selected.HasValue : finished,
                () => completed(selected)));
        }

        private IEnumerator Wait(int run, Func<bool> ready, Action completed)
        {
            do { yield return null; } while (run == generation && !ready());
            if (run != generation) yield break;
            pending = null;
            completed();
        }

        public void Dispose()
        {
            generation++;
            if (pending != null && coroutineHost) coroutineHost.StopCoroutine(pending);
            pending = null;
        }
    }

}
