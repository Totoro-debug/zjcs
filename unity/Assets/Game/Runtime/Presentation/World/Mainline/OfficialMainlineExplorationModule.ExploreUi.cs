using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;
using Zjcs.Official.WorldUI;
using static Zjcs.Offline.Progression.OfficialWorldExplorationCatalog;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private sealed partial class OfficialMainlineExplorationModule
        {
            public readonly List<OfficialMap11WitchWindow> exploreWindows = new List<OfficialMap11WitchWindow>();
            public GameObject exploreCanvas;
            private List<Dictionary<string, object>> exploreUiComponents;
            private int exploreToken;
            private Dictionary<string, object> ExploreFields(string panel, string type, string path = null)
            {
                exploreUiComponents ??= Values(Object(OfficialEquipmentJson.Parse(Resources.Load<TextAsset>(host.NativeUiResource("catalog")).text, true)), "components")
                    .Select(Object).ToList();
                return Object(exploreUiComponents.Single(v => Text(v, "panel") == panel && Text(v, "type") == type
                    && (path == null || Text(v, "path") == path))["fields"]);
            }
            private OfficialMap11WitchWindow NewExploreWindow(string key)
            {
                host.NativeUi.Text("explore_confirm");
                if (exploreCanvas == null)
                {
                    exploreCanvas = new GameObject("Official exploration UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                    exploreCanvas.transform.SetParent(host.uiParent, false);
                    var rect = (RectTransform)exploreCanvas.transform;
                    rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                    rect.sizeDelta = Vector2.zero; rect.anchoredPosition = Vector2.zero;
                    var canvas = exploreCanvas.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.overrideSorting = true; canvas.sortingOrder = 120;
                    var scaler = exploreCanvas.GetComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = .5f;
                }
                var window = new OfficialMap11WitchWindow(key, exploreCanvas.transform, host.NativeUi.Data, host.MapProfile.NativeUiRoot);
                exploreWindows.Add(window);
                window.Root.SetActive(false);
                foreach (var number in host.NativeUi.Data.components.Where(v => v.panel == key && v.type == "RuleTipNumer"))
                {
                    var fields = ExploreFields(key, number.type, number.path);
                    FormatExploreText(window, Text(fields, "mMultiLanguageText"), Int(fields, "mNumber"));
                }
                window.PrepareShow = show => host.StartCoroutine(PrepareExploreMask(window, show));
                foreach (var close in host.NativeUi.Data.components.Where(v => v.panel == key && v.type == "CloseButton"))
                    window.BindButton(close.path, () => CloseExploreWindow(window));
                return window;
            }
            private void CloseExploreWindow(OfficialMap11WitchWindow window)
            {
                exploreWindows.Remove(window);
                var popup = window.Root.GetComponent<WorldPopupWindow>();
                if (popup != null && window.Root.activeInHierarchy)
                    popup.Hide(() => { ReleaseExploreMask(window); window.Destroy(); });
                else { ReleaseExploreMask(window); window.Destroy(); }
            }
            public void CloseExploreUi()
            {
                if (exploreWindows.Count > 0) CloseExploreWindow(exploreWindows[exploreWindows.Count - 1]);
            }
            private void FormatExploreText(OfficialMap11WitchWindow window, string path, params object[] args)
            {
                string key = host.NativeUi.Data.components.Single(v => v.panel == window.Key && v.path == path && v.type == "MultiLanguageText").fields.myKey;
                window.SetText(path, string.Format(CultureInfo.CurrentCulture, window.TextOrKey(key), args));
            }
            private string ExploreRuleText(string key)
            {
                string value = host.NativeUi.Text(key);
                var reset = DateTime.ParseExact(ExploreService.Catalog.Data.settings.DailyResetTime, "(HH:mm:ss)", CultureInfo.InvariantCulture);
                return value.Replace("{MaxMapExploreCountOneDay}", OfficialPropFormat.Compact(ExploreService.Catalog.Data.dailyLimit))
                    .Replace("{DailyResetTime}", string.Format(CultureInfo.CurrentCulture, host.NativeUi.Text("time_format"), reset.Hour, reset.Minute.ToString("D2")))
                    .Replace("{TimeZone}", string.Empty); // Native region zero suppresses the time zone label.
            }
            private void ExploreItem(Transform transform, int item)
            {
                var assets = Resources.Load<OfficialMap11UiAssets>(host.NativeUiResource("DynamicItems"));
                WorldItemIcon icon = transform.GetComponent<WorldItemIcon>();
                icon.Init(assets.Item(item), assets.ItemQuality(item), OfficialPropFormat.Compact(host.progressionSave.InventoryCount(item)));
                if (icon.BottomGo != null) icon.BottomGo.SetActive(false);
            }
            private void SetExploreRequire(OfficialMap11WitchWindow window, string path, int item, long required)
            {
                ExploreItem(window.Find(path), item);
                var fields = ExploreFields(window.Key, "ItemIcon", path);
                string progressPath = Text(fields, "mProgress");
                var progress = window.Find(progressPath);
                progress.gameObject.SetActive(true);
                var source = ExploreFields(window.Key, "ItemProgress", progressPath);
                window.State(host.NativeUi.Data.components.Single(v => v.panel == window.Key && v.type == "ItemProgress" && v.path == progressPath), 0);
                long owned = host.progressionSave.InventoryCount(item);
                var left = window.Find(Text(source, "mLeftCountText")).GetComponent<TMP_Text>();
                var right = window.Find(Text(source, "mRightCountText")).GetComponent<TMP_Text>();
                left.text = OfficialPropFormat.Compact(owned); right.text = OfficialPropFormat.Compact(required);
                float[] rgba = owned >= required ? host.NativeUi.Data.itemProgressEnoughColor : host.NativeUi.Data.itemProgressLackColor;
                left.color = new Color(rgba[0], rgba[1], rgba[2], rgba[3]);
                var color = Object(source["mDefaultTextColor"]);
                right.color = new Color(Convert.ToSingle(color["r"]), Convert.ToSingle(color["g"]), Convert.ToSingle(color["b"]), Convert.ToSingle(color["a"]));
                window.Find(path).GetComponent<WorldItemIcon>().myTextGo.SetActive(false);
            }
            private void ShowExploreConfirm(int times)
            {
                if (exploreWindows.Count != 0 || !ExploreService.CanExplore(ExploreContext())) return;
                var window = NewExploreWindow("ExploreConfirmDialog");
                var fields = ExploreFields(window.Key, window.Key);
                var confirm = ExploreFields(window.Key, "ConfirmDialog");
                window.SetText(Text(confirm, "TitleText"), window.Text("confirmation"));
                window.SetText(Text(confirm, "ConfirmText"), window.Text("confirm"));
                window.SetText(Text(confirm, "CancelText"), window.Text("cancel"));
                var context = ExploreContext();
                window.SetText(Text(confirm, "ContentText"), string.Format(CultureInfo.CurrentCulture, window.Text("explore_confirm"),
                    ExploreService.Catalog.Data.settings.ExploreFoodCost * times, window.Text("item_115_name"),
                    window.Text("region_" + host.mapClassId + "_" + context.Region), host.navigation.currentGrid.X, host.navigation.currentGrid.Y));
                window.Find(Text(confirm, "HintGo")).gameObject.SetActive(false);
                window.Find(Text(confirm, "LockMaskWithTime")).gameObject.SetActive(false);
                window.BindButton(Text(confirm, "CancelBtn"), () => CloseExploreWindow(window));
                window.BindButton(Text(confirm, "ConfirmBtn"), () =>
                {
                    int token = exploreToken;
                    CloseExploreWindow(window);
                    ExecuteExplore(times, token);
                });
                SetExploreRequire(window, Text(fields, "ItemIcon"), 115, times * ExploreService.Catalog.Data.settings.ExploreFoodCost);
                FormatExploreText(window, Text(fields, "mTodayExploredCountText"), ExploreService.DailyCount(ExploreNow), ExploreService.Catalog.Data.dailyLimit);
                exploreToken = 0;
                bool showTokens = times == 1 && host.progressionSave.Save.World.Exploration.TotalCount != 0 && ExploreService.FunctionOpen(51, context);
                window.Find(Text(fields, "TokenIcon")).gameObject.SetActive(false);
                window.Find(Text(fields, "NoneIcon")).gameObject.SetActive(showTokens);
                long ownedTokens = ExploreService.Catalog.Data.tokenGroups.Where(v => v.ItemId > 0).Sum(v => host.progressionSave.InventoryCount(v.ItemId));
                window.Find(Text(fields, "BlueDotGo")).gameObject.SetActive(showTokens && ownedTokens > 0);
                window.SetText(Text(fields, "CountText"), ownedTokens > 99 ? "99..." : ownedTokens.ToString(CultureInfo.InvariantCulture));
                Action select = () => ShowExploreTokens(window, fields);
                window.BindButton(Text(fields, "SelectNewBtn"), select); window.BindButton(Text(fields, "ChangeOldBtn"), select);
                window.BindButton(Text(fields, "mProbabilityBtn"), () => ShowExploreDetail(exploreToken));
                window.Show();
            }
            private void ShowExploreTokens(OfficialMap11WitchWindow confirm, Dictionary<string, object> confirmFields)
            {
                var tokens = ExploreService.Catalog.Data.tokenGroups.Where(v => v.ItemId > 0 && host.progressionSave.InventoryCount(v.ItemId) > 0)
                    .OrderByDescending(v => ExploreService.Catalog.Data.items.Single(item => item.ClassId == v.ItemId).Quality).ToArray();
                if (tokens.Length == 0) { host.hud.ShowOfficialNotice(host.NativeUi.Text("explore_none_token_tip")); return; }
                var window = NewExploreWindow("ExploreSelectTokenDialog");
                var fields = ExploreFields(window.Key, window.Key);
                LoopVerticalScrollRect scroll = window.Find(Text(fields, "ScrollRect")).GetComponent<LoopVerticalScrollRect>();
                var rows = new Dictionary<GameObject, OfficialMap11WitchWindow>();
                int selected = exploreToken;
                scroll.ClearCells();
                scroll.dataSource = new ExploreTokenDataSource((transform, index) =>
                {
                    var token = tokens[index];
                    var row = new OfficialMap11WitchWindow("ExploreSelectTokenDialogItem", transform.gameObject, host.NativeUi.Data, host.MapProfile.NativeUiRoot);
                    var source = ExploreFields(row.Key, row.Key);
                    foreach (Transform child in row.Find(Text(source, "Slot"))) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                    var icon = new OfficialMap11WitchWindow("ItemIconDefault", row.Find(Text(source, "Slot")), host.NativeUi.Data, host.MapProfile.NativeUiRoot);
                    ExploreItem(icon.Root.transform, token.ItemId);
                    row.Find(Text(source, "CanNotUseObj")).gameObject.SetActive(false); // All pinned RequireType values are None.
                    row.Find(Text(source, "CheckedObj")).gameObject.SetActive(selected == token.ItemId);
                    row.BindButton(Text(source, "Button"), () =>
                    {
                        selected = token.ItemId;
                        foreach (var item in rows.Values) item.Find(Text(source, "CheckedObj")).gameObject.SetActive(item == row);
                    });
                    rows[transform.gameObject] = row; row.Show();
                });
                scroll.totalCount = tokens.Length;
                var prepare = window.PrepareShow;
                window.PrepareShow = show => prepare(() => { show(); scroll.RefillCells(); });
                window.BindButton(Text(fields, "ConfirmButton"), () =>
                {
                    exploreToken = selected;
                    confirm.Find(Text(confirmFields, "TokenIcon")).gameObject.SetActive(selected != 0);
                    confirm.Find(Text(confirmFields, "NoneIcon")).gameObject.SetActive(selected == 0);
                    if (selected != 0) SetExploreRequire(confirm, Text(confirmFields, "TokenIcon"), selected, 1);
                    CloseExploreWindow(window);
                });
                window.Show();
            }
            private sealed class ExploreTokenDataSource : LoopScrollDataSource
            {
                private readonly Action<Transform, int> provide;
                public ExploreTokenDataSource(Action<Transform, int> provide) { this.provide = provide; }
                public void ProvideData(Transform transform, int index) => provide(transform, index);
            }
        }
    }
}
