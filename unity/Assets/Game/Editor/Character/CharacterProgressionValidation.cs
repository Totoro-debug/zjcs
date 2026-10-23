using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Zjcs.Offline.Progression;
using UnityEditor;
using UnityEngine;

namespace Zjcs.Editor.Character
{
    [InitializeOnLoad]
    public static class CharacterProgressionValidation
    {
        private const string AssetCatalogPath = "Assets/Game/Resources/Character/OfficialCharacterAssetCatalog.asset";
        private static readonly string[] CatalogPaths =
        {
            "Assets/Official/Character/character-overview.json",
            "Assets/Official/Character/character-properties.json",
            "Assets/Official/Character/character-progression.json",
            "Assets/Official/Character/item-time-limits.json",
            "Assets/Official/CharacterCreation/wardrobe.json",
            "Assets/Official/CharacterCreation/wardrobe-rules.json",
            "Assets/Official/CharacterCreation/wardrobe-ui.json",
            "Assets/Official/Character/character-panels.json",
            "Assets/Official/Character/source-manifest.json",
            "Assets/Official/Skills/skill-runtime-rules.json",
            "Assets/Official/Skills/skills.json"
        };
        private static readonly string[] CatalogFields =
        {
            "characterOverview", "characterProperties", "progressionRules", "timedAppearanceItems",
            "wardrobeItems", "wardrobeRules", "wardrobeUi", "characterPanels", "sourceManifest",
            "skillRules", "skillRecommendations"
        };
        private const string SpriteRoot = "Assets/Official/Character/Art/";

        static CharacterProgressionValidation()
        {
            EditorApplication.delayCall += BindMissingOfficialCatalogs;
        }

        [Serializable]
        private sealed class CharacterSourceManifest
        {
            public string manifest;
            public CharacterSourceOutput[] outputs;
        }

        [Serializable]
        private sealed class CharacterSourceOutput
        {
            public string name;
            public string sha256;
        }

        [MenuItem("Zjcs/Character/Bind Official Catalogs")]
        public static void BindOfficialCatalogs()
        {
            var assets = new TextAsset[CatalogPaths.Length];
            for (int i = 0; i < CatalogPaths.Length; i++)
            {
                assets[i] = AssetDatabase.LoadAssetAtPath<TextAsset>(CatalogPaths[i]);
                if (assets[i] == null)
                    throw new InvalidOperationException("Official character TextAsset was not imported: " + CatalogPaths[i]);
            }

            EnsureFolder("Assets/Game/Resources/Character");
            OfficialCharacterAssetCatalog catalog = AssetDatabase.LoadAssetAtPath<OfficialCharacterAssetCatalog>(AssetCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<OfficialCharacterAssetCatalog>();
                AssetDatabase.CreateAsset(catalog, AssetCatalogPath);
            }
            var serialized = new SerializedObject(catalog);
            for (int i = 0; i < assets.Length; i++)
            {
                SerializedProperty field = serialized.FindProperty(CatalogFields[i]);
                if (field == null) throw new InvalidOperationException("Character catalog field is missing: " + CatalogFields[i]);
                field.objectReferenceValue = assets[i];
            }
            Font uiFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Official/Commerce/Art/fonts/FZY4K.ttf");
            if (uiFont == null)
                throw new InvalidOperationException("The official character UI font was not imported.");
            SerializedProperty fontField = serialized.FindProperty("uiFont");
            if (fontField == null) throw new InvalidOperationException("Character catalog font field is missing.");
            fontField.objectReferenceValue = uiFont;
            string[] spriteGuids = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Official/Character/Art" });
            var spritePaths = spriteGuids.Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.StartsWith(SpriteRoot, StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            if (spritePaths.Length == 0)
                throw new InvalidOperationException("Official character sprites have not been imported.");
            SerializedProperty spriteArray = serialized.FindProperty("sprites");
            if (spriteArray == null) throw new InvalidOperationException("Character sprite binding field is missing.");
            spriteArray.arraySize = spritePaths.Length;
            for (int i = 0; i < spritePaths.Length; i++)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePaths[i]);
                if (sprite == null) throw new InvalidOperationException("Character sprite failed to import: " + spritePaths[i]);
                SerializedProperty binding = spriteArray.GetArrayElementAtIndex(i);
                binding.FindPropertyRelative("sourcePath").stringValue = spritePaths[i].Substring(SpriteRoot.Length).Replace('\\', '/');
                binding.FindPropertyRelative("sprite").objectReferenceValue = sprite;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            OfficialCharacterCatalog loaded = OfficialCharacterAssetCatalog.LoadDefault().Load();
            if (loaded.CumulativeLevelExperience.Count < 2 || loaded.LevelLimits.Count == 0
                || loaded.BlessLevels.Count == 0 || loaded.Appearances.Count == 0 || loaded.Skins.Count == 0)
                throw new InvalidOperationException("Official character catalog failed structural validation.");
            int firstSubRank = loaded.LevelLimits.Min(value => value.SubRank);
            int limit0 = OfflineCharacterExperience.BaseLevelLimit(firstSubRank, loaded.LevelLimits);
            Require(limit0 > 0 && limit0 < loaded.CumulativeLevelExperience.Count,
                "Read the first sub-rank level limit");
            long capExperience = loaded.CumulativeLevelExperience[limit0];
            CharacterExperienceSnapshot atCap = OfflineCharacterExperience.Calculate(
                capExperience, 0, firstSubRank, 1, limit0, false, true, false, 0,
                loaded.CumulativeLevelExperience, loaded.LevelLimits, loaded.BlessLevels);
            Require(atCap.Level == limit0 && atCap.IsStoredExperience && !atCap.IsBlessing,
                "Cross-subrank experience is capped and shown as stored at the rank limit");
            List<CharacterBlessLevelRecord> blessRows = loaded.BlessLevels
                .Where(value => value.Rank == 1).OrderBy(value => value.Exp).ToList();
            if (blessRows.Count > 1 && blessRows[1].BlessLevel > 0)
            {
                CharacterBlessLevelRecord row = blessRows[1];
                CharacterExperienceSnapshot blessed = OfflineCharacterExperience.Calculate(
                    capExperience, row.Exp + 1, firstSubRank, 1, limit0, true, false, false, row.BlessLevel + 1,
                    loaded.CumulativeLevelExperience, loaded.LevelLimits, loaded.BlessLevels);
                Require(blessed.IsBlessing && !blessed.IsStoredExperience
                    && blessed.BlessLevel == row.BlessLevel && blessed.Level == limit0 + row.BlessLevel,
                    "Season blessing level and actual level follow the official bless table");
            }
            IReadOnlyDictionary<int, long> baseProps = loaded.CalcCharacterBaseBattleProperties(1, 1);
            Require(baseProps.TryGetValue(loaded.PropertyIds["BaseMaxHp"], out long hp) && hp == 300
                && baseProps.TryGetValue(loaded.PropertyIds["BaseAttack"], out long attack) && attack == 60
                && baseProps.TryGetValue(loaded.PropertyIds["BaseDefence"], out long defence) && defence == 60
                && baseProps.TryGetValue(loaded.PropertyIds["BaseSpeed"], out long speed) && speed == 48,
                "Load official level, subrank and role-group properties in order");
            ValidateCharacterSourceHashes(assets[Array.IndexOf(CatalogFields, "sourceManifest")].text);
            foreach (string source in new[]
            {
                "res://assets/official/sprites/CharacterRank_1-45973bcfb0.png",
                "res://assets/official/sprites/CharacterRank_2-6c4bafd0d3.png"
            })
                if (catalog.FindSprite(source) == null)
                    throw new InvalidOperationException("Official character rank icon is not bound: " + source);
            Debug.Log("Official character catalogs validated: " + loaded.Appearances.Count
                + " wardrobe entries, " + loaded.Skins.Count + " appearance skins, " + spritePaths.Length
                + " official sprites, and pinned 854_156383 progression tables.");
        }

        private static void BindMissingOfficialCatalogs()
        {
            OfficialCharacterAssetCatalog catalog = AssetDatabase.LoadAssetAtPath<OfficialCharacterAssetCatalog>(AssetCatalogPath);
            if (catalog != null)
            {
                var serialized = new SerializedObject(catalog);
                bool complete = CatalogFields.All(name =>
                {
                    SerializedProperty field = serialized.FindProperty(name);
                    return field != null && field.objectReferenceValue != null;
                });
                SerializedProperty spriteArray = serialized.FindProperty("sprites");
                complete &= spriteArray != null && spriteArray.arraySize > 0;
                for (int i = 0; complete && i < spriteArray.arraySize; i++)
                    complete = spriteArray.GetArrayElementAtIndex(i).FindPropertyRelative("sprite").objectReferenceValue != null;
                int imported = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Official/Character/Art" }).Length;
                complete &= spriteArray != null && spriteArray.arraySize == imported;
                if (complete) return;
            }

            try
            {
                BindOfficialCatalogs();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }

        private static void ValidateCharacterSourceHashes(string json)
        {
            CharacterSourceManifest manifest = JsonUtility.FromJson<CharacterSourceManifest>(json);
            if (manifest == null || manifest.manifest != "854_156383" || manifest.outputs == null)
                throw new InvalidOperationException("Character source manifest is not pinned to 854_156383.");
            foreach (CharacterSourceOutput output in manifest.outputs)
            {
                string path = "Assets/Official/Character/" + output.name;
                string absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, path.Substring("Assets/".Length)));
                if (output.sha256 == null || output.sha256.Length != 64 || !File.Exists(absolutePath)
                    || !string.Equals(Hash(File.ReadAllBytes(absolutePath)), output.sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Character payload hash differs from its source manifest: " + output.name);
            }
        }

        private static string Hash(byte[] bytes)
        {
            using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(bytes);
                return string.Concat(digest.Select(value => value.ToString("x2")));
            }
        }

        [MenuItem("Zjcs/Validate/Character Experience")]
        public static void Run()
        {
            var levels = new long[] { 0, 100, 250, 450 };
            var limits = new[]
            {
                new CharacterLevelLimitRecord { SubRank = 0, PlayerLevel = 2, PlayerBlessLevel = 0 },
                new CharacterLevelLimitRecord { SubRank = 1, PlayerLevel = 3, PlayerBlessLevel = 0 }
            };
            var blessings = new[]
            {
                new CharacterBlessLevelRecord { Rank = 1, BlessLevel = 0, Exp = 0 },
                new CharacterBlessLevelRecord { Rank = 1, BlessLevel = 1, Exp = 50 }
            };

            CharacterExperienceSnapshot ordinary = OfflineCharacterExperience.Calculate(
                150, 0, 0, 1, 2, false, false, false, 0, levels, limits, blessings);
            Require(ordinary.Level == 1 && ordinary.CurrentExperience == 50
                && ordinary.RequiredExperience == 150 && ordinary.Fill > 0f && ordinary.Fill < 1f,
                "Calculate current-level ordinary experience from the official cumulative table");

            CharacterExperienceSnapshot stored = OfflineCharacterExperience.Calculate(
                250, 0, 0, 1, 2, true, true, false, 1, levels, limits, blessings);
            Require(stored.IsStoredExperience && !stored.IsBlessing
                && stored.CurrentExperience == 0 && stored.RequiredExperience == 200,
                "Show stored experience before blessing progress at the base rank cap");

            CharacterExperienceSnapshot maxBless = OfflineCharacterExperience.Calculate(
                250, 50, 0, 1, 2, true, true, true, 1, levels, limits, blessings);
            Require(!maxBless.IsStoredExperience && maxBless.IsBlessing && maxBless.IsMaxBlessing
                && maxBless.Level == 3,
                "Show the actual blessing level and season limit after storage is unavailable");

            Require(OfflineCharacterExperience.ExtendTimedAppearance(1000, 1200, 2, 5, 3, true) == 19000,
                "Extend repeatable timed appearance duration and clamp to maximum duration");
            Require(OfflineCharacterExperience.ExtendTimedAppearance(1000, 1200, 2, 5, 3, false) == 8200,
                "Reset non-overlapping timed appearance duration from the current time");

            var propertyIds = new Dictionary<string, int>
            {
                { "BaseAttack", 1 }, { "BaseAttackRate", 2 }, { "Attack", 3 },
                { "AttackPercent", 4 }, { "AttackScale", 5 }
            };
            var rawProperties = new Dictionary<int, long>
            {
                { 1, 101 }, { 2, 1000 }, { 4, 5000 }, { 5, 5000 }
            };
            Dictionary<int, long> processed = OfficialCharacterProperties.ProcessBattleProperties(rawProperties, propertyIds);
            Require(processed[3] == 249 && !processed.ContainsKey(1) && !processed.ContainsKey(5),
                "Apply official base-rate, percent and scale steps with integer truncation");

            var wardrobe = new WardrobeAppearanceState
            {
                Sex = 0,
                Profession = 1,
                BaseSkinByType = new Dictionary<int, int> { { 5, 500 }, { 6, 600 } },
                AppendedItemByType = new Dictionary<int, int> { { 5, 10 }, { 6, 20 } },
                WearTimeByType = new Dictionary<int, long> { { 5, 1 }, { 6, 2 } }
            };
            var wardrobeItems = new Dictionary<int, WardrobeItemDefinition>
            {
                { 10, new WardrobeItemDefinition { ItemId = 10, ItemType = 319, SkinBySex = new Dictionary<int, int> { { 0, 100 } } } },
                { 20, new WardrobeItemDefinition { ItemId = 20, ItemType = 330, SkinBySex = new Dictionary<int, int> { { 0, 200 } } } }
            };
            var wardrobeSkins = new Dictionary<int, WardrobeSkinDefinition>
            {
                { 100, new WardrobeSkinDefinition { SkinId = 100, Type = 5, BlockedSkinIds = new[] { 200 } } },
                { 200, new WardrobeSkinDefinition { SkinId = 200, Type = 6, BlockedSkinIds = new[] { 100 } } },
                { 500, new WardrobeSkinDefinition { SkinId = 500, Type = 5 } },
                { 600, new WardrobeSkinDefinition { SkinId = 600, Type = 6 } }
            };
            var allowedSkins = new HashSet<string>
            {
                "0:1:100", "0:1:200", "0:1:500", "0:1:600"
            };
            WardrobeResolution resolved = OfflineWardrobeRules.Resolve(wardrobe, wardrobeItems, wardrobeSkins, allowedSkins);
            Require(resolved.SkinByType[5] == 100 && resolved.SkinByType[6] == 600
                && resolved.MutuallySuppressedTypes.Contains(6),
                "Resolve mutually exclusive skins by original wear time and restore the suppressed base appearance");

            ValidatePropertyPipeline();

            Debug.Log("Character progression validation passed: experience, base properties, wardrobe mutex, timed appearance extension and the property pipeline.");
        }

        private static void ValidatePropertyPipeline()
        {
            OfficialCharacterAssetCatalog assetCatalog = OfficialCharacterAssetCatalog.LoadDefault();
            OfficialCharacterCatalog loaded = assetCatalog.Load();
            OfficialPropCatalog props = loaded.Props;
            Require(props.Count >= 300 && props.ByCatalog((int)OfficialPropCatalogType.Main).Count == 4
                && props.ByCatalog((int)OfficialPropCatalogType.Addition).Count > 80,
                "Load the official prop_cfg catalogs, main rows and addition rows");
            Require(props.Categories.Count >= 12 && props.Id("Attack") == 1 && props.Id("BaseMaxHp") == 50,
                "Load official property categories and property identifiers");
            Require(props.Get(1).Name.Length > 0 && props.Get(1).Catalog == (int)OfficialPropCatalogType.Main,
                "Resolve the localized property name and catalog of attack");

            var calculator = new CharacterPropCalculator(loaded);
            var player = new OfflinePlayerProgress
            {
                Level = 1,
                SubRank = 1,
                Wardrobe = new OfflineWardrobeProgress { Profession = 0, AppearanceLevel = 0 }
            };
            CharacterPropSnapshot snapshot = calculator.Evaluate(player, 0);
            Require(CharacterPropFormula.Raw(snapshot.OutOfFightBattleProps, props.Id("MaxHp")) == 300
                && CharacterPropFormula.Raw(snapshot.OutOfFightBattleProps, props.Id("Attack")) == 60
                && CharacterPropFormula.Raw(snapshot.OutOfFightBattleProps, props.Id("Defence")) == 60
                && CharacterPropFormula.Raw(snapshot.OutOfFightBattleProps, props.Id("Speed")) == 48,
                "Run the official base-rate, percent and scale steps over the level, sub-rank and role-group properties");
            Require(snapshot.CombatRating > 0 && snapshot.BattleProps.ContainsKey(props.Id("BaseMaxHp")),
                "Calculate a positive combat rating from the base property chunk");
            Require(snapshot.DetailProps.TryGetValue(CharacterPropDetailCategory.CharacterBase,
                    out IReadOnlyDictionary<int, long> baseDetail)
                && CharacterPropFormula.Raw(baseDetail, props.Id("Attack")) == 60,
                "Build the character-base detailed property source with the original display steps");

            Require(OfficialPropFormat.Compact(12345) == "1.23万" && OfficialPropFormat.Compact(9999) == "9999"
                && OfficialPropFormat.Percent(5000) == "50%" && OfficialPropFormat.Percent(555) == "5.55%",
                "Format property values with the original CN number and percent rules");
            Require(OfficialPropFormat.Value(props.Get(1), 60) == "60"
                && OfficialPropFormat.Value(props.Get(58), 5000) == "50%",
                "Format an integer property and a percent property through prop_cfg");
            Require(loaded.ItemIconUri(43003).Length > 0 && loaded.FindItem(43003) != null,
                "Resolve the official rank material icon and item row");
            Zjcs.Offline.Skills.OfflineSkillRules skillRules = assetCatalog.LoadSkillRules();
            Require(skillRules.Skills.Length == 394
                && loaded.FindItem(10000) != null && loaded.ItemIconUri(10000).Length > 0,
                "Load the official skill definitions, skill item and skill icon");
            OfficialSkillRecommendationSet recommendations = assetCatalog.LoadSkillRecommendations();
            var skillIds = new HashSet<int>(skillRules.Skills.Select(value => value.ClassId));
            Require(recommendations.recommendations.Length > 0
                && recommendations.recommendations.All(value => value != null
                    && loaded.ProfessionName(value.Profession).Length > 0
                    && value.ActiveSkillList.All(skillIds.Contains)
                    && value.PassiveSkillList.All(skillIds.Contains)),
                "Load official skill recommendations with valid professions and skill ids");
            Require(loaded.FindItem(5001) != null && loaded.ItemIconUri(5001).Length > 0
                && loaded.FindItem(41300) != null && loaded.ItemIconUri(41300).Length > 0,
                "Load official gem and equipment upgrade material items with icons");
            Require(loaded.ProfessionName(1).Length > 0 && loaded.ProfessionIconUri(1).Length > 0,
                "Load the official profession name and icon");
            Require(loaded.SkillMergePiece(10000) == 10 && loaded.SkillMergePiece(10001) > 0,
                "Load the official skill merge piece counts");
            Require(loaded.Rows("system_level_limit").Any(row => Convert.ToInt32(row["SubRank"],
                    System.Globalization.CultureInfo.InvariantCulture) == 1
                    && Convert.ToInt32(row["PlayerLevel"], System.Globalization.CultureInfo.InvariantCulture) == 10)
                && loaded.Rows("player_subrank").Count == 40,
                "Read the official rank level limit and sub-rank divisions");
        }

        private static void Require(bool condition, string step)
        {
            if (!condition) throw new InvalidOperationException("Character progression validation failed: " + step);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            string name = path.Substring(separator + 1);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
