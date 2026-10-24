using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Cooking;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Cooking
{
    public sealed class CookingFeatureRuntime : MonoBehaviour
    {
        private OfflineCookingService service;
        private CookingPanelRenderer view;
        private Func<float> maxHpProvider;
        private Action onClosed;
        private List<CookingEntry> entries = new List<CookingEntry>();
        private int selectedRecipeId;
        private int selectedFormulaId;
        private int selectedListItemId;
        private long batchAmount = 1;
        private string notice = string.Empty;
        private bool isClosing;

        public bool IsSelect { get; private set; }
        public int SelectedItemId { get; private set; }
        public event Action<int> ItemSelectedForTutorial;
        public event Action<int, long> CookingCompletedForTutorial;

        public void ClosePanel() => Close();

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        public static CookingFeatureRuntime Open(OfflineMainlineProgressSession profile, Transform canvasParent,
            Func<float> maxHpProvider, Action onClosed = null)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (canvasParent == null) throw new ArgumentNullException(nameof(canvasParent));
            if (maxHpProvider == null) throw new ArgumentNullException(nameof(maxHpProvider));
            EnsureEventSystem();
            var owner = new GameObject("CookingFeatureRuntime", typeof(RectTransform));
            owner.transform.SetParent(canvasParent, false);
            var runtime = owner.AddComponent<CookingFeatureRuntime>();
            try { runtime.Initialize(profile, maxHpProvider, onClosed); }
            catch
            {
                Destroy(owner);
                throw;
            }
            return runtime;
        }

        public Transform GetDefaultRecoveryItem()
        {
            if (service == null || view == null) return null;
            int id = service.GetBestUnlockedRecoveryCookingId(maxHpProvider);
            return id > 0 ? view.RecipeRow(id) : null;
        }

        public bool SelectItem(int itemId)
        {
            CookingEntry entry = entries.FirstOrDefault(value => value.ItemId == itemId);
            if (entry == null) return false;
            SelectEntry(entry);
            return true;
        }

        public CookingResult CraftSelected()
        {
            if (selectedRecipeId <= 0) return new CookingResult
            {
                Status = CookingResultStatus.InvalidSelection,
                Message = "请选择料理。"
            };
            CookingResult result = service.Craft(selectedRecipeId, batchAmount);
            notice = result.IsApplied
                ? "制作完成：" + service.GetItem(result.ItemId)?.name + " × " + result.Count
                : result.Message;
            if (result.IsApplied) CookingCompletedForTutorial?.Invoke(result.ItemId, result.Count);
            RefreshView();
            return result;
        }

        public CookingResult LearnSelectedFormula()
        {
            if (selectedFormulaId <= 0) return new CookingResult
            {
                Status = CookingResultStatus.InvalidSelection,
                Message = "请选择配方。"
            };
            CookingResult result = service.LearnFormula(selectedFormulaId);
            notice = result.IsApplied
                ? "已学会：" + service.GetItem(result.ItemId)?.name
                : result.Message;
            if (result.IsApplied)
            {
                selectedFormulaId = 0;
                selectedListItemId = 0;
            }
            RefreshView();
            return result;
        }

        private void Initialize(OfflineMainlineProgressSession profile, Func<float> hpProvider, Action closeCallback)
        {
            maxHpProvider = hpProvider;
            onClosed = closeCallback;
            OfficialCookingCatalog catalog = OfficialCookingCatalog.LoadDefault();
            service = new OfflineCookingService(profile, catalog);
            view = new CookingPanelRenderer(BindButton);
            view.Create(transform);
            view.EnsureBatchSlider(value => SetBatchAmount(Mathf.RoundToInt(value)));
            RefreshView();
        }

        private void BindButton(string path, GameObject buttonObject)
        {
            Button button = buttonObject == null ? null : buttonObject.GetComponent<Button>();
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            if (path.StartsWith("CookingEntry:", StringComparison.Ordinal)
                && int.TryParse(path.Substring("CookingEntry:".Length), out int entryId))
            {
                button.onClick.AddListener(() => SelectItem(entryId));
                return;
            }
            switch (buttonObject.name)
            {
                case "MakeBtn":
                    button.onClick.AddListener(OnPrimaryAction);
                    break;
                case "CommonBtn_Reduce":
                    button.onClick.AddListener(() => SetBatchAmount(batchAmount - 1));
                    break;
                case "CommonBtn_Add":
                    button.onClick.AddListener(() => SetBatchAmount(batchAmount + 1));
                    break;
                case "MaxBtn":
                    button.onClick.AddListener(() => SetBatchAmount(SelectedMaxBatch()));
                    break;
                case "CloseBtn_dark":
                    button.onClick.AddListener(Close);
                    break;
            }
        }

        private void OnPrimaryAction()
        {
            if (selectedFormulaId > 0) LearnSelectedFormula();
            else CraftSelected();
        }

        private void SelectItem(CookingEntry entry)
        {
            notice = string.Empty;
            selectedListItemId = entry.ItemId;
            if (entry.Kind == CookingEntryKind.Formula)
            {
                selectedFormulaId = entry.ItemId;
                selectedRecipeId = 0;
                SelectedItemId = 0;
                IsSelect = false;
            }
            else
            {
                selectedFormulaId = 0;
                selectedRecipeId = entry.RecipeId;
                SelectedItemId = entry.RecipeId;
                IsSelect = true;
                batchAmount = 1;
                ItemSelectedForTutorial?.Invoke(entry.RecipeId);
            }
            RefreshView();
        }

        private void SelectEntry(CookingEntry entry) => SelectItem(entry);

        private void SetBatchAmount(long value)
        {
            long maximum = SelectedMaxBatch();
            batchAmount = Math.Max(1, Math.Min(Math.Max(1, maximum), value));
            RefreshDetail();
        }

        private long SelectedMaxBatch()
        {
            OfficialCookingRecipe recipe = selectedRecipeId > 0 ? service.GetRecipe(selectedRecipeId) : null;
            return recipe == null || recipe.conditionType == "AlwaysCondition" ? 0 : service.MaxBatch(recipe);
        }

        private void RefreshView()
        {
            if (service == null || view == null) return;
            float maxHp = GetMaxHp();
            entries = service.GetVisibleEntries(maxHp).ToList();
            if (selectedRecipeId > 0 && entries.All(value => value.RecipeId != selectedRecipeId))
            {
                selectedRecipeId = 0;
                SelectedItemId = 0;
                IsSelect = false;
            }
            if (selectedFormulaId > 0 && entries.All(value => value.ItemId != selectedFormulaId))
                selectedFormulaId = 0;

            view.RenderRows(entries, EntryLabel, _ => null, selectedListItemId);
            view.SetNodeActive("EmptyGo", "/EmptyGo", entries.Count == 0);
            RenderSelectedItem();
            RefreshDetail();
        }

        private void RenderSelectedItem()
        {
            IReadOnlyList<Transform> slots = view.MaterialSlots();
            foreach (Transform slot in slots) slot.gameObject.SetActive(false);
            Transform output = view.FindNode("ItemIconDefault", "/FoodMaterial/FoodGo/ItemIconDefault")?.transform;
            if (output != null) output.gameObject.SetActive(false);

            if (selectedFormulaId > 0)
            {
                OfficialCookingItem formula = service.GetItem(selectedFormulaId);
                if (output != null)
                {
                    output.gameObject.SetActive(true);
                    view.RenderIcon(output, formula.id, formula.quality);
                }
                view.SetMakeLabel("学习配方");
                view.SetButton("MakeBtn", true);
                view.SetBatchSlider(0, 1);
                view.SetRecipeCount(1);
                return;
            }

            OfficialCookingRecipe recipe = selectedRecipeId > 0 ? service.GetRecipe(selectedRecipeId) : null;
            if (recipe == null)
            {
                view.SetMakeLabel(null);
                view.SetButton("MakeBtn", false);
                view.SetBatchSlider(0, 1);
                view.SetSelectedCount(1);
                return;
            }
            OfficialCookingItem item = service.GetItem(recipe.id);
            if (output != null)
            {
                output.gameObject.SetActive(true);
                long outputCount = checked((long)recipe.count * batchAmount);
                view.RenderIcon(output, recipe.id, item?.quality ?? 0);
                view.SetRecipeCount(outputCount);
            }
            for (int index = 0; index < slots.Count && index < recipe.materials.Count; index++)
            {
                Transform slot = slots[index];
                OfficialCookingMaterial material = recipe.materials[index];
                OfficialCookingItem materialItem = service.GetItem(material.itemId);
                long inventoryCount = service.InventoryCount(material.itemId);
                long requested = checked(material.count * batchAmount);
                slot.gameObject.SetActive(true);
                view.RenderIcon(slot, material.itemId, materialItem?.quality ?? 0);
                view.SetMaterialCount(slot, inventoryCount + "/" + requested);
            }
            view.SetMakeLabel(null);
            long maxBatch = SelectedMaxBatch();
            view.SetBatchSlider(maxBatch, batchAmount);
        }

        private void RefreshDetail()
        {
            if (view == null || service == null) return;
            if (selectedFormulaId > 0)
            {
                OfficialCookingItem formula = service.GetItem(selectedFormulaId);
                OfficialCookingRecipe recipe = service.Catalog.TryGetFormulaRecipe(selectedFormulaId, out OfficialCookingRecipe value)
                    ? value : null;
                view.SetText("DetailText", "/FoodMaterial/DetailText", "配方纸：" + (formula?.name ?? "")
                    + (recipe == null ? string.Empty : "\n学习后解锁：" + (service.GetItem(recipe.id)?.name ?? recipe.id.ToString()))
                    + (string.IsNullOrEmpty(notice) ? string.Empty : "\n" + notice));
                view.SetSelectedCount(1);
                return;
            }
            OfficialCookingRecipe selected = selectedRecipeId > 0 ? service.GetRecipe(selectedRecipeId) : null;
            OfficialCookingItem item = selected == null ? null : service.GetItem(selected.id);
            string description = selected == null ? "选择料理查看材料与效果。"
                : BuildRecipeDescription(selected, item);
            if (!string.IsNullOrEmpty(notice)) description += "\n" + notice;
            view.SetText("DetailText", "/FoodMaterial/DetailText", description);
            view.SetSelectedCount(batchAmount);
            bool canCraft = selected != null && selected.conditionType != "AlwaysCondition"
                && batchAmount > 0 && batchAmount <= SelectedMaxBatch();
            view.SetMakeLabel(null);
            view.SetButton("MakeBtn", canCraft);
            view.SetBatchSlider(SelectedMaxBatch(), batchAmount);
        }

        private string EntryLabel(CookingEntry entry)
        {
            OfficialCookingItem item = service.GetItem(entry.ItemId);
            return item == null ? entry.ItemId.ToString() : item.name;
        }

        private static string BuildRecipeDescription(OfficialCookingRecipe recipe, OfficialCookingItem item)
        {
            var lines = new List<string> { item?.name ?? recipe.id.ToString() };
            foreach (OfficialCookingEffect effect in recipe.effects)
            {
                if (effect.kind == "CookingExtraInfoRecovery")
                {
                    string value = effect.curePercent > 0
                        ? (effect.curePercent * 100f).ToString("0.##") + "%" : string.Empty;
                    if (effect.cureValue > 0) value += (value.Length == 0 ? string.Empty : " + ") + effect.cureValue.ToString("0.##");
                    lines.Add("恢复 " + value);
                }
                else if (effect.kind == "CookingExtraInfoStatus")
                    lines.Add("状态效果 ID " + effect.statusEffectId);
                else if (effect.kind == "CookingExtraInfoProp")
                    lines.Add("属性效果 ID " + effect.propId);
                else
                    lines.Add(effect.kind + " (" + effect.type + ")");
            }
            return string.Join("\n", lines);
        }

        private float GetMaxHp()
        {
            try
            {
                float value = maxHpProvider?.Invoke() ?? 0;
                return value > 0 && !float.IsNaN(value) && !float.IsInfinity(value) ? value : 0;
            }
            catch { return 0; }
        }

        private void Close()
        {
            if (isClosing) return;
            isClosing = true;
            onClosed?.Invoke();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            view?.Close();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            new GameObject("CookingEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }
}
