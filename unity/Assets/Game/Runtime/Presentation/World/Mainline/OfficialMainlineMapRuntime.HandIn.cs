using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Presentation.Inventory;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        [Serializable]
        private sealed class HandInStoryFields
        {
            public int HandleItemId;
            public int NeedHandInWindow;
        }

        [Serializable]
        private sealed class HandInLocalization
        {
            public string manifest;
            public int mapClassId;
            public OfficialMap11UiText[] texts;
        }

        private static readonly Dictionary<string, string> Map14HandInTexts =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private GameObject handInDialogRoot;
        private OfficialMap14HandInDialogView handInDialog;
        private OfficialInventoryAssetCatalog handInInventoryAssets;

        private void RunHandInItemNode(Map10RuntimeStoryNode node)
        {
            if (mapClassId != 14) throw new InvalidDataException("Hand-in nodes are not migrated for Map" + mapClassId + ".");
            if (!handInItems.TryGetValue(node.handInItemId, out Map13RuntimeHandInItem config))
                throw new InvalidDataException("Official Map14 hand-in configuration is missing: " + node.handInItemId);
            HandInStoryFields fields = JsonUtility.FromJson<HandInStoryFields>(node.sourceJson);
            if (fields == null || fields.HandleItemId != config.Id || fields.NeedHandInWindow < 0
                || fields.NeedHandInWindow > 1 || config.Items.Length != 1)
                throw new InvalidDataException("Official Map14 hand-in node differs from its configured transaction.");

            if (fields.NeedHandInWindow == 0)
            {
                FinishHandIn(node, config, StoryNext(node, "ComfirmExit"), StoryNext(node, "CancelExit"));
                return;
            }

            if (handInDialogRoot != null)
                throw new InvalidOperationException("An official Map14 hand-in dialog is already active.");
            handInInventoryAssets ??= OfficialInventoryAssetCatalog.LoadDefault();
            int itemId = config.Items[0].ItemId;
            Dictionary<string, object> item = progression.EquipmentCatalog.Find("item", itemId);
            if (item == null) throw new InvalidDataException("Official hand-in item is missing from the local item table: " + itemId);
            TextAsset localizationSource = Resources.Load<TextAsset>("Map14/UI/HandIn/catalog");
            HandInLocalization localization = localizationSource == null ? null
                : JsonUtility.FromJson<HandInLocalization>(localizationSource.text);
            if (localization == null || localization.manifest != "871_156598"
                || localization.mapClassId != 14 || localization.texts == null)
                throw new InvalidDataException("Pinned official hand-in UI localization is missing.");
            string UiText(string key)
            {
                if (Map14HandInTexts.TryGetValue(key, out string value)) return value;
                OfficialMap11UiText entry = localization.texts.SingleOrDefault(text => text != null && text.key == key);
                if (entry == null || string.IsNullOrEmpty(entry.value))
                    throw new InvalidDataException("Official hand-in UI localization key is missing: " + key);
                Map14HandInTexts.Add(key, entry.value);
                return entry.value;
            }

            GameObject prefab = Resources.Load<GameObject>("Map14/UI/HandIn/DialogHandInItemDialog");
            if (prefab == null) throw new InvalidDataException("Official Map14 hand-in dialog prefab is missing.");
            handInDialogRoot = new GameObject("Map14HandInUI", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            handInDialogRoot.transform.SetParent(uiParent, false);
            Canvas canvas = handInDialogRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 130;
            CanvasScaler scaler = handInDialogRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = .5f;
            GameObject owner = handInDialogRoot;
            handInDialog = Instantiate(prefab, owner.transform, false).GetComponent<OfficialMap14HandInDialogView>();
            if (handInDialog == null) throw new InvalidDataException("Offline hand-in dialog adapter is missing from the official prefab.");

            long requiredCount = config.Items[0].Count;
            handInDialog.Initialize(handInInventoryAssets, itemId, Convert.ToInt32(item["Quality"], CultureInfo.InvariantCulture),
                requiredCount, StoryItemCount(itemId), UiText,
                () => FinishHandIn(node, config, StoryNext(node, "ComfirmExit"), StoryNext(node, "CancelExit")),
                () => FinishHandInBranch(node, "CancelExit"),
                () => FinishHandInBranch(node, "InterruptExit"),
                () => { if (owner != null) Destroy(owner); });
        }

        private void FinishHandIn(Map10RuntimeStoryNode node, Map13RuntimeHandInItem config,
            int successNode, int failureNode)
        {
            string eventId = MapReceiptPrefix + slotId + ":hand-in:" + activeStoryRootKey + ":" + node.id;
            ProgressionResult result = progressionSave.SubmitHandInItem(config.Id, config.CanHandTimes,
                config.Items.ToDictionary(value => value.ItemId, value => value.Count),
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(), eventId);
            int target = result.Status == ProgressionResultStatus.Applied
                || result.Status == ProgressionResultStatus.Unchanged ? successNode : failureNode;
            CloseHandInView(false);
            if (target > 0) RunStoryGraph(target);
            else FinishStoryGraph();
        }

        private void FinishHandInBranch(Map10RuntimeStoryNode node, string port)
        {
            if (closed || !storyRunning)
            {
                CloseHandInView();
                return;
            }
            int target = StoryNext(node, port);
            CloseHandInView(false);
            if (target > 0) RunStoryGraph(target);
            else FinishStoryGraph();
        }

        private void CloseHandInView(bool destroy = true)
        {
            GameObject root = handInDialogRoot;
            handInDialogRoot = null;
            handInDialog = null;
            if (destroy && root != null) Destroy(root);
        }
    }
}
