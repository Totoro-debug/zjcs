using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.World
{
    /// <summary>Offline binding of AwardListPanel.Update/ShowNextItem and AwardListPanelItem.Init.</summary>
    internal sealed class OfficialMapAwardList : MonoBehaviour
    {
        private readonly Queue<KeyValuePair<int, long>> queue = new Queue<KeyValuePair<int, long>>();
        private OfficialMap11WitchWindow panel;
        private OfficialMap11UiData data;
        private OfficialMap11UiAssets assets;
        private string uiRoot;
        private Transform slot;
        private GameObject context;
        private float elapsed, interval, closeDelay;

        public static OfficialMapAwardList Create(Transform parent, OfficialMap11UiData data, string uiRoot)
        {
            var context = new GameObject("MapAwardUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            context.transform.SetParent(parent, false);
            var rect = (RectTransform)context.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            var canvas = context.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 120;
            var scaler = context.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = .5f;
            var window = new OfficialMap11WitchWindow("AwardListPanel", context.transform, data, uiRoot);
            var result = window.Root.AddComponent<OfficialMapAwardList>();
            result.panel = window;
            result.context = context;
            result.data = data;
            result.uiRoot = uiRoot;
            result.assets = Resources.Load<OfficialMap11UiAssets>(uiRoot + "DynamicItems");
            var fields = window.Source("AwardListPanel").fields;
            var config = fields.mAwardListPanelItemConfigDict.mSerializeBuffer.Single(value => value.Key == 0).Value;
            result.slot = window.Find(data.components.Single(value => value.panel == "AwardListPanel"
                && value.path == config.Path && value.type == "AwardListPanelNormalPath").fields.mSlot);
            result.interval = fields.mIntervalTime;
            result.closeDelay = config.DelayCloseTime;
            return result;
        }

        public void Enqueue(IReadOnlyDictionary<int, long> awards)
        {
            foreach (var award in awards.Where(value => value.Value > 0)) queue.Enqueue(award);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (queue.Count > 0)
            {
                if (elapsed < interval) return;
                var award = queue.Dequeue();
                var item = new OfficialMap11WitchWindow("AwardListPanelItem", slot, data, uiRoot);
                var fields = item.Source("AwardListPanelItem").fields;
                item.State(item.Source("AwardListPanelItem"), 0);
                int qualityId = assets.ItemQualityId(award.Key);
                var light = item.Find(fields.mLightImage).GetComponent<Image>();
                light.gameObject.SetActive(qualityId >= 4);
                if (qualityId >= 4) light.sprite = assets.AwardLight(qualityId);
                string name = data.texts.Single(v => v.key == "item_" + award.Key + "_name").value;
                item.SetText(fields.mItemNameText, award.Value > 1
                    ? string.Format(CultureInfo.CurrentCulture, item.Text("ui_award_list_tip"), name, OfficialPropFormat.Compact(award.Value)) : name);
                var wrapper = new OfficialMap11WitchWindow("ItemAwardIcon", item.Find(fields.mSlot), data, uiRoot);
                wrapper.State(wrapper.Source("ItemAwardIcon"), 0);
                var icon = Instantiate(Resources.Load<GameObject>(uiRoot + assets.ItemTemplate(award.Key)),
                    wrapper.Find(wrapper.Source("ItemAwardIcon").fields.Anchor), false).GetComponent<WorldItemIcon>();
                icon.Init(assets.Item(award.Key), assets.ItemQuality(award.Key), string.Empty);
                icon.myTextGo.SetActive(false);
                if (icon.BottomGo != null) icon.BottomGo.SetActive(false);
                var animation = item.Root.GetComponent<WorldAwardAnimationEvents>();
                if (animation == null) throw new InvalidOperationException("Original award ReturnPool animation is unbound.");
                animation.Returned = () => Destroy(item.Root);
                elapsed = 0;
            }
            else if (elapsed >= closeDelay) Destroy(context);
        }
    }
}
