using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Commerce;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private sealed partial class OfficialMainlineHudModule
        {
            public Map11RuntimeQuickBuy[] quickBuys;
            public WorldCostItemConfirmDialog quickBuyDialog;
            public GameObject quickBuyCanvas;

            private void OpenQuickBuyCurrencyExchange(long shortage, Action completed)
            {
                commerceScreen = CommerceScreenController.OpenMoneyExchange(host.slotId, host.uiParent, shortage, () =>
                {
                    commerceScreen = null;
                    host.progressionSave.Reload();
                    if (!host.closed && host.session != null) completed?.Invoke();
                }, host.persistentDataPath, parent =>
                {
                    GameObject root = Instantiate(Resources.Load<GameObject>(host.NativeUiResource("PayMoneyChangeDialog")), parent, false);
                    WorldPayMoneyChangeDialog dialog = root.GetComponent<WorldPayMoneyChangeDialog>();
                    OfficialMap11UiAssets assets = Resources.Load<OfficialMap11UiAssets>(host.NativeUiResource("DynamicItems"));
                    dialog.Init(string.Format(CultureInfo.InvariantCulture, host.NativeUi.Text("UI_Pay_Exchange_Diamond_Confirm"), shortage),
                        string.Format(CultureInfo.InvariantCulture, host.NativeUi.Text("ui_ItemIcon_count_key"), shortage),
                        string.Format(CultureInfo.InvariantCulture, host.NativeUi.Text("ui_ItemIcon_count_key"), shortage),
                        assets.Item(3), assets.Item(2), assets.quality, host.NativeUi.Text("item_3_name"), host.NativeUi.Text("item_2_name"));
                    return root;
                });
            }

            public void RunStoryQuickBuy(Map10RuntimeStoryNode node)
            {
                StoryValueFields fields = StoryFields(node);
                Map11RuntimeQuickBuy row = quickBuys.SingleOrDefault(value => value.ItemId == fields.ItemId);
                if (row == null || fields.RequireCount <= 0 || !QuickBuyConditionMet(row))
                {
                    ShowOfficialNotice(host.NativeUi.Text("ItemInfoDialog.Limit.QuickBuyConditionNotMet"));
                    host.RunStoryGraph(StoryNext(node, "Failure"));
                    return;
                }
                // These pinned map quotes are one item per fixed price with no refresh tiers.
                if (row.ItemId != fields.ItemId || row.CurrencyId <= 0 || row.BaseCount != 1 || row.RefreshType != 0
                    || row.SubRankScaleId != 0 || row.LevelScaleId != 0
                    || row.PriceList == null || row.PriceList.Length != 1 || row.PriceList[0] <= 0
                    || row.PriceBuyCount == null || row.PriceBuyCount.Length != 1 || row.PriceBuyCount[0] != -1)
                    throw new InvalidDataException("Map" + host.mapClassId + " quick-buy configuration requires an unsupported quote rule.");
                long buyCount = Math.DivRem(fields.RequireCount, row.BaseCount, out long remainder);
                if (remainder > 0) buyCount++;
                long award = checked(buyCount * row.BaseCount);
                long price = checked(buyCount * row.PriceList[0]);
                OfficialMap11UiAssets assets = Resources.Load<OfficialMap11UiAssets>(host.NativeUiResource("DynamicItems"));
                quickBuyCanvas = new GameObject("Map11QuickBuyUI", typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasScaler), typeof(GraphicRaycaster));
                quickBuyCanvas.transform.SetParent(host.uiParent, false);
                Canvas canvas = quickBuyCanvas.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 130;
                CanvasScaler scaler = quickBuyCanvas.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 0.5f;
                quickBuyDialog = Instantiate(Resources.Load<GameObject>(host.NativeUiResource("CostItemConfirmDialog")),
                    quickBuyCanvas.transform, false).GetComponent<WorldCostItemConfirmDialog>();
                int next = 0;
                quickBuyDialog.Init(host.NativeUi.Text("confirmation"), string.Format(CultureInfo.InvariantCulture,
                        host.NativeUi.Text("GachaPanel.ReplaceRaffleTicket"), fields.RequireCount,
                        host.NativeUi.Text("item_" + row.ItemId + "_name"), price, host.NativeUi.Text("item_" + row.CurrencyId + "_name")),
                    host.NativeUi.Text("confirm"), host.NativeUi.Text("cancel"), string.Format(CultureInfo.InvariantCulture,
                        host.NativeUi.Text("common_format_args1"), price), assets.Item(row.CurrencyId), host.StoryItemCount(row.CurrencyId) >= price,
                    () =>
                    {
                        host.progressionSave.Reload();
                        if (host.StoryItemCount(row.CurrencyId) < price)
                        {
                            if (host.mapClassId == 13)
                            {
                                next = StoryNext(node, "Failure");
                                return;
                            }
                            OpenQuickBuyCurrencyExchange(price - host.StoryItemCount(row.CurrencyId),
                                () => host.RunStoryGraph(StoryNext(node, "Failure")));
                            return;
                        }
                        if (!QuickBuyConditionMet(row))
                        {
                            next = StoryNext(node, "Failure");
                            return;
                        }
                        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        ProgressionResult result = host.progressionSave.ApplyInventoryTransaction(
                            host.MapReceiptPrefix + host.slotId + ":quickbuy:" + Guid.NewGuid().ToString("N"), save =>
                            {
                                if (!OfflineInventoryOperations.Consume(save.Player, row.CurrencyId, price, now).IsApplied
                                    || !OfflineInventoryOperations.Grant(save.Player, row.ItemId, award, now).IsApplied)
                                    throw new InvalidOperationException("Quick-buy inventory transaction failed.");
                            });
                        if (result.Status != ProgressionResultStatus.Applied) throw new InvalidOperationException(result.Message);
                        next = StoryNext(node, "Success");
                    }, () => next = StoryNext(node, "Failure"), () =>
                    {
                        Destroy(quickBuyCanvas);
                        quickBuyDialog = null;
                        quickBuyCanvas = null;
                        if (next > 0) host.RunStoryGraph(next);
                    });
                WorldItemIcon icon = Instantiate(Resources.Load<GameObject>(host.NativeUiResource("ItemIconDefault")), quickBuyDialog.Slot, false)
                    .GetComponent<WorldItemIcon>();
                icon.Init(assets.Item(row.ItemId), assets.quality,
                    string.Format(CultureInfo.InvariantCulture, host.NativeUi.Text("ui_ItemIcon_count_key"), award));
            }

            private bool QuickBuyConditionMet(Map11RuntimeQuickBuy row)
            {
                if (row.Condition == null) return true;
                if (row.Condition._type != "HasItemCondition" || row.Condition.Op != ">=")
                    throw new InvalidDataException("Unsupported Map11 quick-buy condition.");
                return host.StoryItemCount(row.Condition.ClassId) >= row.Condition.Count;
            }
        }
    }
}
