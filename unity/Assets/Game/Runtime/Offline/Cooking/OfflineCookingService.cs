using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.Progression;

namespace Zjcs.Offline.Cooking
{
    public enum CookingEntryKind
    {
        Recipe,
        Formula
    }

    public sealed class CookingEntry
    {
        public CookingEntryKind Kind;
        public int ItemId;
        public int RecipeId;
        public bool IsBestRecovery;
        public bool CanCraft;
        public long MaxBatch;
    }

    public enum CookingResultStatus
    {
        Applied,
        InvalidSelection,
        Locked,
        Disabled,
        MissingMaterials,
        InvalidAmount,
        SaveRejected
    }

    public sealed class CookingResult
    {
        public CookingResultStatus Status;
        public int ItemId;
        public long Count;
        public string Message;
        public bool IsApplied => Status == CookingResultStatus.Applied;
    }

    public sealed class OfflineCookingService
    {
        public const int CookingFormulaItemType = 322;
        public const int CookingFormulaSourceOperate = 89;

        private readonly OfflineMainlineProgressSession profile;
        private readonly OfficialCookingCatalog catalog;

        public OfflineCookingService(OfflineMainlineProgressSession profile, OfficialCookingCatalog catalog)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public OfficialCookingCatalog Catalog => catalog;

        public long InventoryCount(int itemId)
        {
            OfflineItemStack stack = profile.Save.Player.Inventory?.FirstOrDefault(value => value != null && value.ItemId == itemId);
            return stack == null ? 0 : stack.Count;
        }

        public long MaxBatch(OfficialCookingRecipe recipe)
        {
            if (recipe == null || recipe.materials == null || recipe.materials.Count == 0) return 0;
            long count = catalog.Data.maxCookingCountAtATime;
            foreach (OfficialCookingMaterial material in recipe.materials)
            {
                long need = material.count;
                if (need <= 0) return 0;
                count = Math.Min(count, InventoryCount(material.itemId) / need);
            }
            return Math.Max(0, count);
        }

        public bool IsUnlocked(OfficialCookingRecipe recipe)
        {
            return recipe != null && IsUnlocked(recipe, profile.Save);
        }

        private static bool IsUnlocked(OfficialCookingRecipe recipe, OfflineMainlineSave save)
        {
            switch (recipe.conditionType)
            {
                case "NoneCondition":
                case "AlwaysCondition":
                    return true;
                case "MapUnlockCondition":
                    return save?.Player?.UnlockedMapClassIds?.Contains(recipe.mapClassId) == true;
                case "UsedItemCondition":
                    return save?.Player?.AppliedInventoryEventIds?.Contains(UsedItemMarker(recipe)) == true;
                default:
                    return false;
            }
        }

        public IReadOnlyList<CookingEntry> GetVisibleEntries(float maxHp)
        {
            int bestRecovery = GetBestUnlockedRecoveryCookingId(maxHp);
            var formulas = new List<CookingEntry>();
            foreach (OfficialCookingItem item in catalog.Items)
            {
                if (item.itemType != CookingFormulaItemType || InventoryCount(item.id) <= 0
                    || !catalog.TryGetFormulaRecipe(item.id, out OfficialCookingRecipe recipe) || IsUnlocked(recipe))
                    continue;
                formulas.Add(new CookingEntry { Kind = CookingEntryKind.Formula, ItemId = item.id, RecipeId = recipe.id });
            }
            formulas.Sort((left, right) =>
            {
                int quality = GetItem(left.ItemId).quality.CompareTo(GetItem(right.ItemId).quality);
                return quality != 0 ? quality : left.ItemId.CompareTo(right.ItemId);
            });

            var recipes = new List<CookingEntry>();
            foreach (OfficialCookingRecipe recipe in catalog.Recipes)
            {
                if (!IsUnlocked(recipe)) continue;
                recipes.Add(new CookingEntry
                {
                    Kind = CookingEntryKind.Recipe,
                    ItemId = recipe.id,
                    RecipeId = recipe.id,
                    IsBestRecovery = recipe.id == bestRecovery,
                    CanCraft = recipe.conditionType != "AlwaysCondition" && MaxBatch(recipe) > 0,
                    MaxBatch = recipe.conditionType == "AlwaysCondition" ? 0 : MaxBatch(recipe)
                });
            }
            recipes.Sort((left, right) => CompareRecipes(left, right, maxHp));
            formulas.AddRange(recipes);
            return formulas;
        }

        public int GetBestUnlockedRecoveryCookingId(Func<float> maxHpProvider)
        {
            if (maxHpProvider == null) return 0;
            float maxHp;
            try { maxHp = maxHpProvider(); }
            catch { return 0; }
            return GetBestUnlockedRecoveryCookingId(maxHp);
        }

        private int GetBestUnlockedRecoveryCookingId(float maxHp)
        {
            if (maxHp <= 0 || float.IsNaN(maxHp) || float.IsInfinity(maxHp)) return 0;
            int bestScore = int.MinValue;
            int bestId = 0;
            foreach (OfficialCookingRecipe recipe in catalog.Recipes)
            {
                if (recipe.type != 0 || !IsUnlocked(recipe)) continue;
                OfficialCookingEffect recovery = recipe.effects.FirstOrDefault(value => value.kind == "CookingExtraInfoRecovery");
                if (recovery == null) continue;
                float score = recovery.curePercent + (float)(recovery.cureValue / maxHp);
                int officialScore = (int)((double)score * 10000.0);
                if (officialScore >= bestScore)
                {
                    bestScore = officialScore;
                    bestId = recipe.id;
                }
            }
            return bestId;
        }

        public CookingResult LearnFormula(int formulaItemId)
        {
            if (!catalog.TryGetFormulaRecipe(formulaItemId, out OfficialCookingRecipe recipe))
                return Result(CookingResultStatus.InvalidSelection, formulaItemId, message: "未找到该配方。");
            if (recipe.conditionType != "UsedItemCondition" || recipe.useSourceOperate != CookingFormulaSourceOperate)
                return Result(CookingResultStatus.Disabled, formulaItemId, message: "该配方的官方解锁条件当前不支持。");

            try { profile.Reload(); }
            catch (Exception exception) { return Result(CookingResultStatus.SaveRejected, formulaItemId, message: exception.Message); }
            if (IsUnlocked(recipe, profile.Save))
                return Result(CookingResultStatus.Locked, formulaItemId, message: "该配方已经学会。");
            if (InventoryCount(formulaItemId) <= 0)
                return Result(CookingResultStatus.InvalidSelection, formulaItemId, message: "配方物品已不在背包中。");

            string marker = UsedItemMarker(recipe);
            string eventId = "cooking:learn:" + formulaItemId + ":" + Guid.NewGuid().ToString("N");
            ProgressionResult transaction = profile.ApplyInventoryTransaction(eventId, save =>
            {
                if (IsUnlocked(recipe, save)) throw new InvalidOperationException("该配方已经学会。");
                InventoryOperationResult consumed = OfflineInventoryOperations.Consume(save.Player, formulaItemId, 1,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                if (!consumed.IsApplied) throw new InvalidOperationException("配方物品已不在背包中。");
                if (!save.Player.AppliedInventoryEventIds.Contains(marker)) save.Player.AppliedInventoryEventIds.Add(marker);
            });
            return transaction != null && transaction.IsApplied
                ? Result(CookingResultStatus.Applied, recipe.id, 1)
                : Result(CookingResultStatus.SaveRejected, formulaItemId, message: transaction?.Message ?? "无法保存配方学习进度。");
        }

        public CookingResult Craft(int recipeId, long batchAmount)
        {
            if (!catalog.TryGetRecipe(recipeId, out OfficialCookingRecipe recipe))
                return Result(CookingResultStatus.InvalidSelection, recipeId, message: "未找到该料理。");
            try { profile.Reload(); }
            catch (Exception exception) { return Result(CookingResultStatus.SaveRejected, recipeId, message: exception.Message); }
            if (!IsUnlocked(recipe)) return Result(CookingResultStatus.Locked, recipeId, message: "尚未解锁该料理。");
            if (recipe.conditionType == "AlwaysCondition")
                return Result(CookingResultStatus.Disabled, recipeId, message: "该料理当前不可制作。");
            if (batchAmount <= 0 || batchAmount > catalog.Data.maxCookingCountAtATime)
                return Result(CookingResultStatus.InvalidAmount, recipeId, message: "制作数量无效。");
            if (MaxBatch(recipe) < batchAmount)
                return Result(CookingResultStatus.MissingMaterials, recipeId, message: "材料不足。");

            long outputCount;
            try { outputCount = checked((long)recipe.count * batchAmount); }
            catch (OverflowException) { return Result(CookingResultStatus.InvalidAmount, recipeId, message: "成品数量超出上限。"); }
            string eventId = "cooking:craft:" + Guid.NewGuid().ToString("N");
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            ProgressionResult transaction = profile.ApplyInventoryTransaction(eventId, save =>
            {
                if (!IsUnlocked(recipe, save))
                    throw new InvalidOperationException("尚未解锁该料理。");
                foreach (OfficialCookingMaterial material in recipe.materials)
                {
                    long required = checked(material.count * batchAmount);
                    InventoryOperationResult consumed = OfflineInventoryOperations.Consume(save.Player,
                        material.itemId, required, now);
                    if (!consumed.IsApplied) throw new InvalidOperationException("材料不足。");
                }
                InventoryOperationResult granted = OfflineInventoryOperations.Grant(save.Player, recipe.id, outputCount, now);
                if (!granted.IsApplied) throw new InvalidOperationException("成品无法加入背包。");
            });
            return transaction != null && transaction.IsApplied
                ? Result(CookingResultStatus.Applied, recipeId, outputCount)
                : Result(CookingResultStatus.SaveRejected, recipeId, message: transaction?.Message ?? "制作结果无法保存。");
        }

        public CookingResult UsePermanentPropertyCooking(int cookingId, long useCount)
        {
            if (!catalog.TryGetRecipe(cookingId, out OfficialCookingRecipe recipe))
                return Result(CookingResultStatus.InvalidSelection, cookingId, message: "未找到该料理。");
            if (!recipe.effects.Any(effect => effect.type == 2 && effect.kind == "CookingExtraInfoProp"))
                return Result(CookingResultStatus.Disabled, cookingId, message: "该料理没有永久属性效果。");
            if (useCount <= 0) return Result(CookingResultStatus.InvalidAmount, cookingId, message: "使用数量无效。");

            try { profile.Reload(); }
            catch (Exception exception) { return Result(CookingResultStatus.SaveRejected, cookingId, message: exception.Message); }
            long availableCount = InventoryCount(cookingId);
            long maxUseCount = catalog.MaxPermanentPropertyUseCount(cookingId, availableCount);
            if (maxUseCount <= 0)
                return Result(CookingResultStatus.Disabled, cookingId, message: "该料理当前不可使用。");
            if (useCount > maxUseCount)
                return Result(availableCount < useCount
                    ? CookingResultStatus.MissingMaterials : CookingResultStatus.InvalidAmount,
                    cookingId, message: "料理使用数量超出当前可用范围。");

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string eventId = "cooking:use:" + cookingId + ":" + Guid.NewGuid().ToString("N");
            ProgressionResult transaction = profile.ApplyInventoryTransaction(eventId, save =>
            {
                InventoryOperationResult consumed = OfflineInventoryOperations.Consume(save.Player,
                    cookingId, useCount, now);
                if (!consumed.IsApplied) throw new InvalidOperationException("料理数量不足。");
                OfflineCharacterPropertyProgress properties = save.Player.CharacterProperties;
                if (properties == null)
                    save.Player.CharacterProperties = properties = new OfflineCharacterPropertyProgress();
                if (properties.CookingUses == null) properties.CookingUses = new List<OfflineCookingUse>();
                OfflineCookingUse usage = properties.CookingUses.FirstOrDefault(value =>
                    value != null && value.CookingId == cookingId);
                if (usage == null)
                    properties.CookingUses.Add(usage = new OfflineCookingUse { CookingId = cookingId });
                usage.UseTimes = checked(usage.UseTimes + useCount);
            });
            return transaction != null && transaction.IsApplied
                ? Result(CookingResultStatus.Applied, cookingId, useCount)
                : Result(CookingResultStatus.SaveRejected, cookingId,
                    message: transaction?.Message ?? "料理使用结果无法保存。");
        }

        public long MaxPermanentPropertyUseCount(int cookingId, long availableCount)
        {
            return catalog.MaxPermanentPropertyUseCount(cookingId, availableCount);
        }

        private int CompareRecipes(CookingEntry left, CookingEntry right, float maxHp)
        {
            int compare = right.IsBestRecovery.CompareTo(left.IsBestRecovery);
            if (compare != 0) return compare;
            compare = right.CanCraft.CompareTo(left.CanCraft);
            if (compare != 0) return compare;
            OfficialCookingItem leftItem = GetItem(left.ItemId);
            OfficialCookingItem rightItem = GetItem(right.ItemId);
            compare = rightItem.quality.CompareTo(leftItem.quality);
            if (compare != 0) return compare;
            OfficialCookingRecipe leftRecipe = catalog.TryGetRecipe(left.RecipeId, out OfficialCookingRecipe a) ? a : null;
            OfficialCookingRecipe rightRecipe = catalog.TryGetRecipe(right.RecipeId, out OfficialCookingRecipe b) ? b : null;
            compare = (rightRecipe?.type ?? 0).CompareTo(leftRecipe?.type ?? 0);
            if (compare != 0) return compare;
            compare = RecoveryScore(rightRecipe, maxHp).CompareTo(RecoveryScore(leftRecipe, maxHp));
            return compare != 0 ? compare : left.ItemId.CompareTo(right.ItemId);
        }

        public OfficialCookingItem GetItem(int id)
        {
            return catalog.TryGetItem(id, out OfficialCookingItem item) ? item : null;
        }

        public OfficialCookingRecipe GetRecipe(int id)
        {
            return catalog.TryGetRecipe(id, out OfficialCookingRecipe recipe) ? recipe : null;
        }

        private static int RecoveryScore(OfficialCookingRecipe recipe, float maxHp)
        {
            if (recipe == null || recipe.type != 0 || maxHp <= 0) return 0;
            OfficialCookingEffect effect = recipe.effects.FirstOrDefault(value => value.kind == "CookingExtraInfoRecovery");
            return effect == null ? 0 : (int)((double)(effect.curePercent + (float)(effect.cureValue / maxHp)) * 10000.0);
        }

        private static string UsedItemMarker(OfficialCookingRecipe recipe)
        {
            return "cooking:used-item:" + recipe.useSourceOperate + ":" + recipe.formulaItemId;
        }

        private static CookingResult Result(CookingResultStatus status, int itemId, long count = 0, string message = null)
        {
            return new CookingResult { Status = status, ItemId = itemId, Count = count, Message = message ?? string.Empty };
        }
    }
}
