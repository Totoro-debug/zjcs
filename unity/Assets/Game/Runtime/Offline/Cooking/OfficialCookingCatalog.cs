using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.Cooking
{
    [Serializable]
    public sealed class OfficialCookingCatalogData
    {
        public string manifest;
        public int maxCookingCountAtATime;
        public int cookingRecoveryCountThreshold;
        public List<OfficialCookingRecipe> recipes = new List<OfficialCookingRecipe>();
        public List<OfficialCookingItem> items = new List<OfficialCookingItem>();
    }

    [Serializable]
    public sealed class OfficialCookingRecipe
    {
        public int id;
        public int count;
        public int type;
        public List<OfficialCookingMaterial> materials = new List<OfficialCookingMaterial>();
        public string conditionType;
        public int mapClassId;
        public int formulaItemId;
        public int useSourceOperate;
        public List<OfficialCookingEffect> effects = new List<OfficialCookingEffect>();
        public string effectIcon;
        public string effectIconColor;
    }

    [Serializable]
    public sealed class OfficialCookingMaterial
    {
        public int itemId;
        public long count;
    }

    [Serializable]
    public sealed class OfficialCookingEffect
    {
        public int type;
        public string kind;
        public float curePercent;
        public double cureValue;
        public int statusEffectId;
        public int propId;
        public List<int> propTypes = new List<int>();
    }

    [Serializable]
    public sealed class OfficialCookingItem
    {
        public int id;
        public string name;
        public string icon;
        public int itemType;
        public int quality;
    }

    public sealed class OfficialCookingCatalog
    {
        public const string ExpectedManifest = "854_156383";
        public const string DefaultRelativePath = "Cooking/cooking.json";

        private readonly Dictionary<int, OfficialCookingRecipe> recipes;
        private readonly Dictionary<int, OfficialCookingItem> items;
        private readonly Dictionary<int, OfficialCookingRecipe> formulaRecipes;

        private OfficialCookingCatalog(OfficialCookingCatalogData data)
        {
            Data = data;
            recipes = data.recipes.ToDictionary(value => value.id);
            items = data.items.ToDictionary(value => value.id);
            formulaRecipes = data.recipes.Where(value => value.formulaItemId > 0)
                .ToDictionary(value => value.formulaItemId);
        }

        public OfficialCookingCatalogData Data { get; }
        public IReadOnlyList<OfficialCookingRecipe> Recipes => Data.recipes;
        public IReadOnlyList<OfficialCookingItem> Items => Data.items;

        public static OfficialCookingCatalog LoadDefault()
        {
            string path = Path.Combine(Application.streamingAssetsPath, DefaultRelativePath);
            if (!File.Exists(path)) throw new FileNotFoundException("Official Cooking data is missing.", path);
            OfficialCookingCatalogData data = JsonUtility.FromJson<OfficialCookingCatalogData>(File.ReadAllText(path));
            if (!IsValid(data)) throw new InvalidDataException("Official Cooking data is invalid or unsupported.");
            return new OfficialCookingCatalog(data);
        }

        public static bool IsValid(OfficialCookingCatalogData data)
        {
            if (data == null || data.manifest != ExpectedManifest || data.maxCookingCountAtATime <= 0
                || data.cookingRecoveryCountThreshold < 0 || data.recipes == null || data.items == null
                || data.recipes.Count != 110 || data.items.Count != 305)
                return false;
            var recipeIds = new HashSet<int>();
            var formulaIds = new HashSet<int>();
            foreach (OfficialCookingRecipe recipe in data.recipes)
            {
                if (recipe == null || recipe.id <= 0 || recipe.count <= 0 || !recipeIds.Add(recipe.id)
                    || recipe.materials == null || recipe.materials.Count == 0 || recipe.effects == null
                    || recipe.formulaItemId < 0 || recipe.useSourceOperate < 0)
                    return false;
                if (recipe.formulaItemId > 0 && !formulaIds.Add(recipe.formulaItemId)) return false;
                foreach (OfficialCookingMaterial material in recipe.materials)
                    if (material == null || material.itemId <= 0 || material.count <= 0) return false;
                if (recipe.conditionType == "MapUnlockCondition" && recipe.mapClassId <= 0) return false;
                if (recipe.conditionType == "UsedItemCondition" && recipe.useSourceOperate <= 0) return false;
                if (recipe.conditionType != "NoneCondition" && recipe.conditionType != "MapUnlockCondition"
                    && recipe.conditionType != "UsedItemCondition" && recipe.conditionType != "AlwaysCondition")
                    return false;
            }
            var itemIds = new HashSet<int>();
            foreach (OfficialCookingItem item in data.items)
                if (item == null || item.id <= 0 || string.IsNullOrEmpty(item.name) || !itemIds.Add(item.id)) return false;
            return recipeIds.All(id => itemIds.Contains(id)) && formulaIds.All(id => itemIds.Contains(id))
                && data.recipes.All(value => value.materials.All(material => itemIds.Contains(material.itemId)));
        }

        public bool TryGetRecipe(int itemId, out OfficialCookingRecipe recipe) => recipes.TryGetValue(itemId, out recipe);
        public bool TryGetItem(int itemId, out OfficialCookingItem item) => items.TryGetValue(itemId, out item);
        public bool TryGetFormulaRecipe(int formulaItemId, out OfficialCookingRecipe recipe)
            => formulaRecipes.TryGetValue(formulaItemId, out recipe);
    }
}
