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
        private Map11RuntimeQuickBuy[] quickBuys;
        private WorldCostItemConfirmDialog quickBuyDialog;
        private GameObject quickBuyCanvas;

        private void OpenQuickBuyCurrencyExchange(long shortage, Action completed)
        {
            commerceScreen = CommerceScreenController.OpenMoneyExchange(slotId, uiParent, shortage, () =>
            {
                commerceScreen = null;
                progressionSave.Reload();
                if (!closed && session != null) completed?.Invoke();
            }, persistentDataPath, parent =>
            {
                GameObject root = Instantiate(Resources.Load<GameObject>(NativeUiResource("PayMoneyChangeDialog")), parent, false);
                WorldPayMoneyChangeDialog dialog = root.GetComponent<WorldPayMoneyChangeDialog>();
                OfficialMap11UiAssets assets = Resources.Load<OfficialMap11UiAssets>(NativeUiResource("DynamicItems"));
                dialog.Init(string.Format(CultureInfo.InvariantCulture, GoddessText("UI_Pay_Exchange_Diamond_Confirm"), shortage),
                    string.Format(CultureInfo.InvariantCulture, GoddessText("ui_ItemIcon_count_key"), shortage),
                    string.Format(CultureInfo.InvariantCulture, GoddessText("ui_ItemIcon_count_key"), shortage),
                    assets.Item(3), assets.Item(2), assets.quality, GoddessText("item_3_name"), GoddessText("item_2_name"));
                return root;
            });
        }

        private void RunStoryQuickBuy(Map10RuntimeStoryNode node)
        {
            StoryValueFields fields = StoryFields(node);
            Map11RuntimeQuickBuy row = quickBuys.SingleOrDefault(value => value.ItemId == fields.ItemId);
            if (row == null || fields.RequireCount <= 0 || !QuickBuyConditionMet(row))
            {
                ShowOfficialNotice(GoddessText("ItemInfoDialog.Limit.QuickBuyConditionNotMet"));
                RunStoryGraph(StoryNext(node, "Failure"));
                return;
            }
            // Pinned Map11 has only this constant, unlimited quote. Guard the
            // complete configuration rather than silently treating new tiers as flat.
            if (row.ItemId != 325 || row.BaseCount != 1 || row.RefreshType != 0
                || row.SubRankScaleId != 0 || row.LevelScaleId != 0
                || row.PriceList.Length != 1 || row.PriceBuyCount.Length != 1 || row.PriceBuyCount[0] != -1)
                throw new InvalidDataException("Map11 quick-buy configuration requires an unsupported quote rule.");
            long buyCount = Math.DivRem(fields.RequireCount, row.BaseCount, out long remainder);
            if (remainder > 0) buyCount++;
            long award = checked(buyCount * row.BaseCount);
            long price = checked(buyCount * row.PriceList[0]);
            OfficialMap11UiAssets assets = Resources.Load<OfficialMap11UiAssets>(NativeUiResource("DynamicItems"));
            quickBuyCanvas = new GameObject("Map11QuickBuyUI", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            quickBuyCanvas.transform.SetParent(uiParent, false);
            Canvas canvas = quickBuyCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 130;
            CanvasScaler scaler = quickBuyCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            quickBuyDialog = Instantiate(Resources.Load<GameObject>(NativeUiResource("CostItemConfirmDialog")),
                quickBuyCanvas.transform, false).GetComponent<WorldCostItemConfirmDialog>();
            int next = 0;
            quickBuyDialog.Init(GoddessText("confirmation"), string.Format(CultureInfo.InvariantCulture,
                    GoddessText("GachaPanel.ReplaceRaffleTicket"), fields.RequireCount,
                    GoddessText("item_" + row.ItemId + "_name"), price, GoddessText("item_" + row.CurrencyId + "_name")),
                GoddessText("confirm"), GoddessText("cancel"), string.Format(CultureInfo.InvariantCulture,
                    GoddessText("common_format_args1"), price), assets.Item(row.CurrencyId), StoryItemCount(row.CurrencyId) >= price,
                () =>
                {
                    progressionSave.Reload();
                    if (StoryItemCount(row.CurrencyId) < price)
                    {
                        OpenQuickBuyCurrencyExchange(price - StoryItemCount(row.CurrencyId),
                            () => RunStoryGraph(StoryNext(node, "Failure")));
                        return;
                    }
                    if (!QuickBuyConditionMet(row))
                    {
                        next = StoryNext(node, "Failure");
                        return;
                    }
                    long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    ProgressionResult result = progressionSave.ApplyInventoryTransaction(
                        MapReceiptPrefix + slotId + ":quickbuy:" + Guid.NewGuid().ToString("N"), save =>
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
                    if (next > 0) RunStoryGraph(next);
                });
            WorldItemIcon icon = Instantiate(Resources.Load<GameObject>(NativeUiResource("ItemIconDefault")), quickBuyDialog.Slot, false)
                .GetComponent<WorldItemIcon>();
            icon.Init(assets.Item(row.ItemId), assets.quality,
                string.Format(CultureInfo.InvariantCulture, GoddessText("ui_ItemIcon_count_key"), award));
        }

        private bool QuickBuyConditionMet(Map11RuntimeQuickBuy row)
        {
            if (row.Condition == null) return true;
            if (row.Condition._type != "HasItemCondition" || row.Condition.Op != ">=")
                throw new InvalidDataException("Unsupported Map11 quick-buy condition.");
            return StoryItemCount(row.Condition.ClassId) >= row.Condition.Count;
        }
    }
}
