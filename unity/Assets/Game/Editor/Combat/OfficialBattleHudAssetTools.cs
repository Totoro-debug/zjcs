using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.TextCore.LowLevel;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.TextCore.LowLevel;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;
using Spine.Unity;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleHudAssetTools
    {
        private const string Payload = "Assets/Official/Combat/HUD/";
        private const string RuntimePath = Payload + "panels-runtime.json";
        private const string OriginalPath = Payload + "panels.json";
        private const string SourceManifestPath = Payload + "panels-runtime-source-manifest.json";
        private const string FontPath = "Assets/Official/Commerce/Art/fonts/FZY4K.ttf";
        private const string ResourcesRoot = "Assets/Game/Resources/Combat";
        private const string FontAssetPath = ResourcesRoot + "/FZY4K-Dynamic.asset";
        private const string CatalogPath = ResourcesRoot + "/OfficialBattleHudCatalog.asset";
        private const string ModelPath = "Assets/Official/Combat/Presentation/models.json";
        private const string ModelManifestPath = "Assets/Official/Combat/Presentation/models-source-manifest.json";
        private const string ModelIdentityPath = "Assets/Official/Combat/Presentation/model-identities.json";
        private const string ModelIdentityManifestPath = "Assets/Official/Combat/Presentation/model-identities-source-manifest.json";
        private const string SpineCatalogPath = "Assets/Official/Combat/Spine/catalog.json";
        private const string ModelCatalogPath = ResourcesRoot + "/OfficialBattleModelCatalog.asset";

        [MenuItem("Zjcs/Combat/Import and Validate Official HUD")]
        public static void ImportAndValidate()
        {
            TextAsset runtime = AssetDatabase.LoadAssetAtPath<TextAsset>(RuntimePath);
            TextAsset original = AssetDatabase.LoadAssetAtPath<TextAsset>(OriginalPath);
            TextAsset sourceManifest = AssetDatabase.LoadAssetAtPath<TextAsset>(SourceManifestPath);
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (runtime == null || original == null || sourceManifest == null || sourceFont == null)
                throw new BuildFailedException("Restore official battle HUD payload and FZY4K font before importing.");
            BattleHudRuntimeSourceManifest manifest = JsonUtility.FromJson<BattleHudRuntimeSourceManifest>(sourceManifest.text);
            if (manifest == null || manifest.manifest != "854_156383" || manifest.output_path != RuntimePath
                || manifest.output_sha256 != Sha256(Encoding.UTF8.GetBytes(runtime.text)))
                throw new BuildFailedException("Official battle HUD runtime payload does not match its source manifest.");
            BattleHudCatalogData data = JsonUtility.FromJson<BattleHudCatalogData>(runtime.text);
            if (data == null || data.manifest != "854_156383" || data.prefabs == null || data.prefabs.Length != 30)
                throw new BuildFailedException("Official battle HUD contains an incomplete prefab set.");
            foreach (BattleHudPrefab prefab in data.prefabs)
                if (prefab == null || prefab.root == null)
                    throw new BuildFailedException("Official battle HUD prefab has no root node.");

            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { Payload + "Sprites" });
            if (textureGuids.Length != 701) throw new BuildFailedException("Expected 701 official battle HUD sprites.");
            var bindings = new BattleHudSpriteBinding[textureGuids.Length];
            var spriteSettings = new System.Collections.Generic.Dictionary<string, BattleHudSprite>();
            foreach (BattleHudPrefab prefab in data.prefabs) CollectSpriteSettings(prefab.root, spriteSettings);
            for (int i = 0; i < textureGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new BuildFailedException("HUD sprite has no texture importer: " + path);
                string file = Path.GetFileName(path);
                spriteSettings.TryGetValue(file, out BattleHudSprite sourceSprite);
                bool updateImporter = importer.textureType != TextureImporterType.Sprite
                    || importer.spriteImportMode != SpriteImportMode.Single;
                if (sourceSprite != null)
                {
                    Vector2 pivot = sourceSprite.pivot != null && sourceSprite.pivot.Length >= 2
                        ? new Vector2(sourceSprite.pivot[0] / Mathf.Max(1, sourceSprite.width), sourceSprite.pivot[1] / Mathf.Max(1, sourceSprite.height))
                        : new Vector2(0.5f, 0.5f);
                    Vector4 border = sourceSprite.border != null && sourceSprite.border.Length >= 4
                        ? new Vector4(sourceSprite.border[0], sourceSprite.border[1], sourceSprite.border[2], sourceSprite.border[3])
                        : Vector4.zero;
                    float ppu = sourceSprite.ppu > 0 ? sourceSprite.ppu : 100f;
                    updateImporter |= importer.spritePivot != pivot || importer.spriteBorder != border
                        || !Mathf.Approximately(importer.spritePixelsPerUnit, ppu);
                    importer.spritePivot = pivot;
                    importer.spriteBorder = border;
                    importer.spritePixelsPerUnit = ppu;
                }
                if (updateImporter)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.mipmapEnabled = false;
                    importer.SaveAndReimport();
                }
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) throw new BuildFailedException("HUD sprite did not import: " + path);
                bindings[i] = new BattleHudSpriteBinding { file = file, sprite = sprite };
            }

            EnsureFolders();
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (font == null)
            {
                font = TMP_FontAsset.CreateFontAsset(sourceFont, 40, 4, GlyphRenderMode.SDFAA,
                    2048, 2048, AtlasPopulationMode.Dynamic, true);
                font.name = "FZY4K SDF - dynamic";
                font.italicStyle = 15;
                font.tabSize = 10;
                AssetDatabase.CreateAsset(font, FontAssetPath);
                if (font.material != null) AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (Texture2D atlas in font.atlasTextures)
                    if (atlas != null) AssetDatabase.AddObjectToAsset(atlas, font);
            }

            OfficialBattleHudCatalog catalog = AssetDatabase.LoadAssetAtPath<OfficialBattleHudCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<OfficialBattleHudCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            SerializedObject serialized = new SerializedObject(catalog);
            serialized.FindProperty("runtimePanels").objectReferenceValue = runtime;
            serialized.FindProperty("originalPanels").objectReferenceValue = original;
            serialized.FindProperty("sourceManifest").objectReferenceValue = sourceManifest;
            serialized.FindProperty("font").objectReferenceValue = font;
            SerializedProperty spriteArray = serialized.FindProperty("sprites");
            spriteArray.arraySize = bindings.Length;
            for (int i = 0; i < bindings.Length; i++)
            {
                SerializedProperty entry = spriteArray.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("file").stringValue = bindings[i].file;
                entry.FindPropertyRelative("sprite").objectReferenceValue = bindings[i].sprite;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            catalog.Load();
            Debug.Log("Official battle HUD imported: 30 prefab trees, 701 sprites, and FZY4K TMP font.");
            ValidateRenderer();
        }

        [MenuItem("Zjcs/Combat/Import Official Battle Models")]
        public static void ImportOfficialBattleModels()
        {
            TextAsset modelAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(ModelPath);
            TextAsset manifestAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(ModelManifestPath);
            TextAsset identityAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(ModelIdentityPath);
            TextAsset identityManifestAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(ModelIdentityManifestPath);
            TextAsset spineCatalogAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(SpineCatalogPath);
            if (modelAsset == null || manifestAsset == null || identityAsset == null
                || identityManifestAsset == null || spineCatalogAsset == null)
                throw new BuildFailedException("Restore official battle model and Spine catalogs before importing.");

            OfficialBattleModelSourceManifest source = JsonUtility.FromJson<OfficialBattleModelSourceManifest>(manifestAsset.text);
            string modelHash = Sha256(Encoding.UTF8.GetBytes(modelAsset.text));
            if (source == null || source.manifest != "854_156383" || source.source_sha256 != modelHash
                || source.output_sha256 != modelHash || source.output_path != ModelPath)
                throw new BuildFailedException("Official battle model catalog does not match its source manifest.");
            OfficialBattleModelIdentitySourceManifest identityManifest =
                JsonUtility.FromJson<OfficialBattleModelIdentitySourceManifest>(identityManifestAsset.text);
            string identityHash = Sha256(Encoding.UTF8.GetBytes(identityAsset.text));
            OfficialBattleModelIdentityData identityData = JsonUtility.FromJson<OfficialBattleModelIdentityData>(identityAsset.text);
            if (identityManifest == null || identityManifest.manifest != "854_156383"
                || identityManifest.output_path != ModelIdentityPath || identityManifest.output_sha256 != identityHash
                || identityData == null || identityData.manifest != "854_156383"
                || identityData.model_source_sha256 != modelHash || identityData.monster_models == null
                || identityData.monster_models.Length != 343
                || identityData.spine_source_manifest_sha256 != identityManifest.source_sha256)
                throw new BuildFailedException("Official monster model identity data does not match its source manifest.");
            var monsterIds = new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal);
            var seenMonsterIds = new System.Collections.Generic.HashSet<int>();
            foreach (OfficialBattleMonsterModel identity in identityData.monster_models)
                if (identity == null || identity.monster_id <= 0 || string.IsNullOrEmpty(identity.key)
                    || !seenMonsterIds.Add(identity.monster_id) || monsterIds.ContainsKey(identity.key))
                    throw new BuildFailedException("Official monster model identity mapping is ambiguous.");
                else monsterIds.Add(identity.key, identity.monster_id);

            var models = Spine.Json.Deserialize(new StringReader(modelAsset.text)) as System.Collections.Generic.Dictionary<string, object>;
            var spineRoot = Spine.Json.Deserialize(new StringReader(spineCatalogAsset.text)) as System.Collections.Generic.Dictionary<string, object>;
            var skeletons = spineRoot == null ? null : spineRoot["skeletons"] as System.Collections.Generic.Dictionary<string, object>;
            if (models == null || skeletons == null || models.Count != 409 || skeletons.Count != 1678)
                throw new BuildFailedException("Official battle model/Spine catalog shape is unsupported.");
            foreach (string monsterKey in monsterIds.Keys)
                if (!models.ContainsKey(monsterKey) || !skeletons.ContainsKey(monsterKey))
                    throw new BuildFailedException("Official monster identity references an unbound Spine model: " + monsterKey);

            var bindings = new System.Collections.Generic.List<OfficialBattleModelBinding>();
            int unresolved = 0;
            foreach (var model in models)
            {
                if (!skeletons.TryGetValue(model.Key, out object skeletonValue))
                {
                    unresolved++;
                    continue;
                }
                var modelAnimations = model.Value as System.Collections.Generic.Dictionary<string, object>;
                var skeletonRecord = skeletonValue as System.Collections.Generic.Dictionary<string, object>;
                var spineAnimations = skeletonRecord["animations"] as System.Collections.Generic.Dictionary<string, object>;
                string file = Convert.ToString(skeletonRecord["file"]);
                string stem = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file));
                string skeletonPath = "Assets/Official/Combat/Spine/" + stem + "_SkeletonData.asset";
                SkeletonDataAsset skeleton = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(skeletonPath);
                if (skeleton == null) throw new BuildFailedException("Official battle SkeletonDataAsset is missing: " + skeletonPath);

                var aliases = new System.Collections.Generic.List<OfficialBattleAnimationAlias>();
                foreach (string name in modelAnimations.Keys)
                {
                    string actual = null;
                    if (spineAnimations.ContainsKey(name)) actual = name;
                    else
                        foreach (string candidate in spineAnimations.Keys)
                            if (candidate.EndsWith("/" + name, StringComparison.Ordinal))
                            {
                                if (actual != null) throw new BuildFailedException("Ambiguous official Spine animation alias: " + model.Key + "/" + name);
                                actual = candidate;
                            }
                    if (actual != null) aliases.Add(new OfficialBattleAnimationAlias { name = name, spineName = actual });
                }
                if (aliases.Count == 0) throw new BuildFailedException("Official battle model has no resolvable animations: " + model.Key);
                bindings.Add(new OfficialBattleModelBinding
                {
                    key = model.Key,
                    monsterId = monsterIds.TryGetValue(model.Key, out int monsterId) ? monsterId : 0,
                    skin = Convert.ToString(skeletonRecord["skin"]),
                    scale = Convert.ToSingle(skeletonRecord["scale"]),
                    animations = aliases.ToArray(),
                    skeleton = skeleton
                });
            }

            EnsureFolders();
            OfficialBattleModelCatalog catalog = AssetDatabase.LoadAssetAtPath<OfficialBattleModelCatalog>(ModelCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<OfficialBattleModelCatalog>();
                AssetDatabase.CreateAsset(catalog, ModelCatalogPath);
            }
            var serialized = new SerializedObject(catalog);
            serialized.FindProperty("sourceModels").objectReferenceValue = modelAsset;
            serialized.FindProperty("sourceManifest").objectReferenceValue = manifestAsset;
            serialized.FindProperty("identities").objectReferenceValue = identityAsset;
            serialized.FindProperty("identitiesManifest").objectReferenceValue = identityManifestAsset;
            SerializedProperty rows = serialized.FindProperty("bindings");
            rows.arraySize = bindings.Count;
            for (int i = 0; i < bindings.Count; i++)
            {
                SerializedProperty row = rows.GetArrayElementAtIndex(i);
                row.FindPropertyRelative("key").stringValue = bindings[i].key;
                row.FindPropertyRelative("monsterId").intValue = bindings[i].monsterId;
                row.FindPropertyRelative("skin").stringValue = bindings[i].skin;
                row.FindPropertyRelative("scale").floatValue = bindings[i].scale;
                row.FindPropertyRelative("skeleton").objectReferenceValue = bindings[i].skeleton;
                SerializedProperty animations = row.FindPropertyRelative("animations");
                animations.arraySize = bindings[i].animations.Length;
                for (int j = 0; j < bindings[i].animations.Length; j++)
                {
                    SerializedProperty animation = animations.GetArrayElementAtIndex(j);
                    animation.FindPropertyRelative("name").stringValue = bindings[i].animations[j].name;
                    animation.FindPropertyRelative("spineName").stringValue = bindings[i].animations[j].spineName;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("Official battle models imported: " + bindings.Count + " Spine models; " + unresolved + " composite player bindings require the official appearance renderer.");
        }

        [MenuItem("Zjcs/Combat/Validate Official HUD Renderer")]
        public static void ValidateRenderer()
        {
            OfficialBattleHudCatalog catalog = OfficialBattleHudCatalog.LoadDefault();
            ValidateStatusIconBindings(catalog);
            TMP_FontAsset font = catalog.Font;
            if (font.material == null || font.atlasTexture == null)
                throw new BuildFailedException("Official battle HUD TMP font material or atlas is missing: " + font.name);
            var canvasObject = new GameObject("BattleHudValidationCanvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var renderer = new OfficialBattleHudRenderer(catalog);
                BattleHudCatalogData data = catalog.Load();
                if (data.prefabs.Length != 30)
                    throw new BuildFailedException("Official battle HUD must contain all 30 prefab trees.");
                int images = 0, texts = 0, buttons = 0, layouts = 0, fitters = 0, elements = 0, scrolls = 0;
                foreach (BattleHudPrefab prefab in data.prefabs)
                {
                    int prefabButtons = 0;
                    GameObject root = renderer.Create(prefab.key, canvasObject.transform, (path, node) =>
                    {
                        Button button = node.GetComponent<Button>();
                        if (button == null) return;
                        prefabButtons++;
                        button.onClick.AddListener(() => { });
                    });
                    int prefabImages = root.GetComponentsInChildren<Image>(true).Length;
                    int prefabTexts = root.GetComponentsInChildren<TMP_Text>(true).Length;
                    int prefabLayouts = root.GetComponentsInChildren<LayoutGroup>(true).Length;
                    int prefabFitters = root.GetComponentsInChildren<ContentSizeFitter>(true).Length;
                    int prefabElements = root.GetComponentsInChildren<LayoutElement>(true).Length;
                    int prefabScrolls = root.GetComponentsInChildren<ScrollRect>(true).Length;
                    int expectedImages = Count(prefab.root, node => node.has_image || node.button);
                    int expectedTexts = Count(prefab.root, node => node.has_text);
                    int expectedButtons = Count(prefab.root, node => node.button);
                    int expectedLayouts = Count(prefab.root, node => node.has_layout);
                    int expectedFitters = Count(prefab.root, node => node.has_size_fitter);
                    int expectedElements = Count(prefab.root, node => node.has_layout_element);
                    int expectedScrolls = Count(prefab.root, node => node.has_scroll);
                    if (prefabImages != expectedImages
                        || prefabTexts != expectedTexts || prefabButtons != expectedButtons
                        || prefabLayouts != expectedLayouts || prefabFitters != expectedFitters
                        || prefabElements != expectedElements || prefabScrolls != expectedScrolls)
                        throw new BuildFailedException("Official HUD component counts differ in " + prefab.key
                            + ": Image " + prefabImages + "/" + expectedImages + ", TMP " + prefabTexts + "/" + expectedTexts
                            + ", Button " + prefabButtons + "/" + expectedButtons + ", LayoutGroup " + prefabLayouts + "/" + expectedLayouts
                            + ", ContentSizeFitter " + prefabFitters + "/" + expectedFitters + ", LayoutElement " + prefabElements + "/" + expectedElements
                            + ", ScrollRect " + prefabScrolls + "/" + expectedScrolls + ".");
                    if (prefab.key == "main" && renderer.Find("/FightMainPanel/Content/Top/FixedContent/RoundTitle/RoundText") == null)
                        throw new BuildFailedException("Official battle HUD round label is missing.");
                    images += prefabImages;
                    texts += prefabTexts;
                    buttons += prefabButtons;
                    layouts += prefabLayouts;
                    fitters += prefabFitters;
                    elements += prefabElements;
                    scrolls += prefabScrolls;
                    UnityEngine.Object.DestroyImmediate(root);
                }
                ValidateDamageFloatDelay(catalog);
                Debug.Log("Official battle HUD renderer validated: 30 prefab trees, " + images + " images, " + texts
                    + " texts, " + buttons + " buttons, " + layouts + " layout groups, " + fitters + " fitters, "
                    + elements + " layout elements, and " + scrolls + " scroll views.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }

        private static void ValidateStatusIconBindings(OfficialBattleHudCatalog catalog)
        {
            TextAsset source = Resources.Load<TextAsset>("Combat/battle-status-icons");
            BattleHudStatusIconCatalogData data = source == null
                ? null : JsonUtility.FromJson<BattleHudStatusIconCatalogData>(source.text);
            if (data == null || data.manifest != "854_156383" || data.statusEntityCount != 1595
                || data.entries == null || data.entries.Length != 715)
                throw new BuildFailedException("Official battle status icon catalog is incomplete.");

            var statusIds = new System.Collections.Generic.HashSet<int>();
            int entriesWithSprites = 0;
            foreach (BattleHudStatusIconBinding entry in data.entries)
            {
                Sprite icon = null;
                Sprite background = null;
                bool showStackCount = false;
                if (entry == null || !statusIds.Add(entry.statusId)
                    || !catalog.TryGetStatusSprites(entry.statusId, out icon, out background, out showStackCount)
                    || showStackCount != entry.showStackCount
                    || (!string.IsNullOrEmpty(entry.iconFile) && icon == null)
                    || (!string.IsNullOrEmpty(entry.backgroundFile) && background == null))
                    throw new BuildFailedException("Official battle status sprite binding is invalid: "
                        + (entry == null ? "<null>" : entry.statusId.ToString()));
                if (icon != null || background != null) entriesWithSprites++;
            }
            if (entriesWithSprites != 714)
                throw new BuildFailedException("Official battle status sprite coverage differs from the source table.");
            Debug.Log("Official battle status sprites validated: " + data.entries.Length
                + " status entries and " + entriesWithSprites + " entries with official sprites.");
        }

        private static void ValidateDamageFloatDelay(OfficialBattleHudCatalog hudCatalog)
        {
            var sceneRoot = new GameObject("BattleDamageFloatTimingValidation");
            var hudParent = new GameObject("BattleDamageFloatCanvasParent", typeof(RectTransform));
            OfficialBattleNativeAnimationLibrary models = OfficialBattleNativeAnimationLibrary.LoadDefault();
            OfficialBattleSceneView scene = null;
            try
            {
                var grid = new FightBattleGrid { Width = 4, Height = 4 };
                var mapper = new OfficialBattleGridMapper(sceneRoot.transform, grid);
                scene = new OfficialBattleSceneView(OfficialBattleModelCatalog.LoadDefault(),
                    sceneRoot.transform,
                    null, mapper, null, (transform, unit) => { }, hudCatalog, hudParent.transform, models);
                var checkpoint = new FightBattleCheckpoint();
                var monster = new FightBattleUnitState
                {
                    Id = 2,
                    MonsterId = 3207,
                    X = 1,
                    Y = 1,
                    Hp = 10,
                    MaxHp = 10
                };
                string modelKey = models.ResolveModelKey(monster);
                OfficialBattleNativeModelRecord model = models.FindModel(modelKey);
                if (model == null || !models.HasAnimation(modelKey, "idle_01")
                    || !models.HasAnimation(modelKey, "run_01")
                    || !models.HasAnimation(modelKey, "skill_01")
                    || !models.HasAnimation(modelKey, "hit_01")
                    || !models.HasAnimation(modelKey, "die_01"))
                    throw new BuildFailedException("Native battle model 3207 is missing a core animation binding.");
                checkpoint.Units.Add(monster);
                var stepEvent = new FightBattleStepEvent();
                stepEvent.DamageEvents.Add(new FightBattleDamageEvent
                {
                    TargetId = 2,
                    Damage = 5,
                    Delay = 0.25f
                });
                scene.ShowDamageFloats(stepEvent, checkpoint, OfficialBattleDamageColors.Load());
                if (hudParent.GetComponentsInChildren<OfficialBattleDamageFloat>(true).Length != 0)
                    throw new BuildFailedException("Battle damage float appeared before its configured hit delay.");
                scene.Tick(0.2f);
                if (hudParent.GetComponentsInChildren<OfficialBattleDamageFloat>(true).Length != 0)
                    throw new BuildFailedException("Battle damage float appeared before the configured hit delay elapsed.");
                scene.Tick(0.06f);
                OfficialBattleDamageFloat[] floats = hudParent.GetComponentsInChildren<OfficialBattleDamageFloat>(true);
                if (floats.Length != 1 || floats[0].EffectType != 0)
                    throw new BuildFailedException("Official normal damage float did not appear after its configured hit delay.");
                BattleHudPrefab normalDamagePrefab = hudCatalog.FindPrefab("damage_BattleDamageFloatEffect");
                if (normalDamagePrefab == null || normalDamagePrefab.damageUi == null)
                    throw new BuildFailedException("Official normal damage float binding is missing.");
                Transform normalDamageNumber = FindPrefabPath(floats[0].transform,
                    normalDamagePrefab.damageUi.numberPath);
                if (normalDamageNumber == null
                    || floats[0].GetComponent<OfficialBattleHudAnimationPlayer>() == null)
                    throw new BuildFailedException("Official normal damage float animation was not initialized.");
                float initialNumberScale = normalDamageNumber.localScale.x;
                scene.Tick(0.05f);
                if (Mathf.Approximately(initialNumberScale, normalDamageNumber.localScale.x))
                    throw new BuildFailedException("Official normal damage float animation did not advance with the battle clock.");
                if (OfficialBattleSceneView.FormatBattleNumber(12345) != "1.23\u4e07"
                    || OfficialBattleSceneView.FormatBattleNumber(100000000) != "1\u4ebf")
                    throw new BuildFailedException("Official Chinese battle damage number formatting differs from the client formatter.");

                BattleHudDamageFloatBinding markerBinding = normalDamagePrefab == null
                    ? null : normalDamagePrefab.damageUi;
                if (markerBinding == null || string.IsNullOrEmpty(markerBinding.trueDamagePath)
                    || string.IsNullOrEmpty(markerBinding.breakResiliencePath))
                    throw new BuildFailedException("Official normal damage float marker paths are missing.");
                var markerEvents = new FightBattleStepEvent();
                markerEvents.DamageEvents.Add(new FightBattleDamageEvent
                {
                    TargetId = 2,
                    Damage = 40,
                    TrueDamage = true
                });
                markerEvents.DamageEvents.Add(new FightBattleDamageEvent
                {
                    TargetId = 2,
                    Damage = 35,
                    BreakResilience = true
                });
                scene.ShowDamageFloats(markerEvents, checkpoint, OfficialBattleDamageColors.Load());
                floats = hudParent.GetComponentsInChildren<OfficialBattleDamageFloat>(true);
                int trueDamageMarkers = 0;
                int breakResilienceMarkers = 0;
                foreach (OfficialBattleDamageFloat value in floats)
                {
                    if (value.EffectType != markerBinding.type) continue;
                    Transform trueMarker = FindPrefabPath(value.transform, markerBinding.trueDamagePath);
                    Transform breakMarker = FindPrefabPath(value.transform, markerBinding.breakResiliencePath);
                    if (trueMarker == null || breakMarker == null)
                        throw new BuildFailedException("Official damage float marker path did not resolve in its prefab.");
                    bool trueVisible = trueMarker.gameObject.activeSelf;
                    bool breakVisible = breakMarker.gameObject.activeSelf;
                    if (trueVisible && breakVisible)
                        throw new BuildFailedException("Official true-damage and break-resilience markers were shown together.");
                    if (trueVisible)
                    {
                        trueDamageMarkers++;
                        if (!HasLocalizedMarkerText(trueMarker, "\u7834\u706d\u4f24\u5bb3"))
                            throw new BuildFailedException("Official true-damage marker text is missing or untranslated.");
                    }
                    if (breakVisible)
                    {
                        breakResilienceMarkers++;
                        if (!HasLocalizedMarkerText(breakMarker, "\u7834\u97e7"))
                            throw new BuildFailedException("Official break-resilience marker text is missing or untranslated.");
                    }
                }
                if (trueDamageMarkers != 1 || breakResilienceMarkers != 1)
                    throw new BuildFailedException("Official hit flags did not activate exactly their matching damage float markers.");

                var specialEvents = new FightBattleStepEvent();
                specialEvents.DamageEvents.Add(new FightBattleDamageEvent { TargetId = 2, Damage = 0, Dodge = true });
                specialEvents.DamageEvents.Add(new FightBattleDamageEvent { TargetId = 2, Damage = 0, Immunity = true });
                specialEvents.DamageEvents.Add(new FightBattleDamageEvent { TargetId = 2, Damage = 0, Blinding = true });
                specialEvents.DamageEvents.Add(new FightBattleDamageEvent { TargetId = 2, Damage = -12345, Critical = true, ElementType = 200 });
                scene.ShowDamageFloats(specialEvents, checkpoint, OfficialBattleDamageColors.Load());
                floats = hudParent.GetComponentsInChildren<OfficialBattleDamageFloat>(true);
                var types = new System.Collections.Generic.HashSet<int>();
                foreach (OfficialBattleDamageFloat value in floats) types.Add(value.EffectType);
                if (!types.Contains(6) || !types.Contains(4) || !types.Contains(8) || !types.Contains(9))
                    throw new BuildFailedException("Official damage float type mapping omitted a critical, immunity, dodge, or blinding prefab.");
                if (hudParent.GetComponentsInChildren<TMP_Text>(true).Length == 0)
                    throw new BuildFailedException("Official damage float prefabs did not render TMP content.");

                BattleHudPrefab resetPrefab = hudCatalog.FindPrefab("damage_FightResetSkillCDFloatEffect");
                if (resetPrefab == null || resetPrefab.damageUi == null || resetPrefab.damageUi.type != 10)
                    throw new BuildFailedException("Official skill cooldown reset float binding is missing.");
                var replayEvents = new FightBattleStepEvent();
                replayEvents.SkillCasts.Add(new FightBattleSkillCastEvent
                {
                    ActorId = monster.Id,
                    Slot = 0,
                    IsReplay = true
                });
                scene.ShowDamageFloats(replayEvents, checkpoint, OfficialBattleDamageColors.Load());
                floats = hudParent.GetComponentsInChildren<OfficialBattleDamageFloat>(true);
                OfficialBattleDamageFloat resetFloat = null;
                foreach (OfficialBattleDamageFloat value in floats)
                    if (value.EffectType == 10) { resetFloat = value; break; }
                if (resetFloat == null || !HasLocalizedMarkerText(resetFloat.transform, "\u91cd\u7f6e\u51b7\u5374"))
                    throw new BuildFailedException("Official skill cooldown reset float did not preserve its localized prefab text.");
                Transform resetAnimationRoot = FindPrefabPath(resetFloat.transform,
                    "/FightResetSkillCDFloatEffect/AnimationRoot");
                if (resetAnimationRoot == null
                    || resetFloat.GetComponent<OfficialBattleHudAnimationPlayer>() == null)
                    throw new BuildFailedException("Official skill cooldown reset float animation was not initialized.");
                Vector2 resetStart = ((RectTransform)resetAnimationRoot).anchoredPosition;
                scene.Tick(0.1f);
                if (Vector2.Distance(resetStart, ((RectTransform)resetAnimationRoot).anchoredPosition) < 0.01f)
                    throw new BuildFailedException("Official skill cooldown reset animation did not advance with the battle clock.");

                var stackRoot = new GameObject("BattleDamageFloatStackValidation");
                var stackHud = new GameObject("BattleDamageFloatStackCanvasParent", typeof(RectTransform));
                stackHud.transform.SetParent(stackRoot.transform, false);
                OfficialBattleSceneView stackScene = null;
                try
                {
                    var stackMapper = new OfficialBattleGridMapper(stackRoot.transform, grid);
                    stackScene = new OfficialBattleSceneView(OfficialBattleModelCatalog.LoadDefault(),
                        stackRoot.transform, null, stackMapper, null, (transform, unit) => { },
                        hudCatalog, stackHud.transform, models);
                    var stackEvents = new FightBattleStepEvent();
                    stackEvents.DamageEvents.Add(new FightBattleDamageEvent { TargetId = 2, Damage = 21 });
                    stackEvents.DamageEvents.Add(new FightBattleDamageEvent { TargetId = 2, Damage = 13 });
                    stackEvents.StatusCustomActions.Add(new FightBattleStatusCustomActionEvent
                    {
                        UnitId = 2,
                        Action = 3
                    });
                    OfficialBattleDamageColors stackColors = OfficialBattleDamageColors.Load();
                    stackScene.ShowDamageFloats(stackEvents, checkpoint, stackColors);
                    OfficialBattleDamageFloat[] stackFloats = stackRoot.GetComponentsInChildren<OfficialBattleDamageFloat>(true);
                    BattleCustomActionWorldRuntimeData customActionPrefab = hudCatalog.LoadCustomActionWorldRuntime();
                    OfficialBattleDamageFloat customActionFloat = null;
                    foreach (OfficialBattleDamageFloat value in stackFloats)
                        if (value.EffectType == customActionPrefab.effectType) customActionFloat = value;
                    Transform customActionTextNode = customActionFloat == null ? null
                        : FindPrefabPath(customActionFloat.transform, customActionPrefab.textPath);
                    Transform customActionIconNode = customActionFloat == null ? null
                        : FindPrefabPath(customActionFloat.transform, customActionPrefab.iconPath);
                    TMP_Text customActionText = customActionTextNode == null
                        ? null : customActionTextNode.GetComponent<TMP_Text>();
                    SpriteRenderer customActionIcon = customActionIconNode == null
                        ? null : customActionIconNode.GetComponent<SpriteRenderer>();
                    if (stackFloats.Length != 3 || customActionFloat == null || customActionText == null
                        || customActionText.text != "减少冷却" || customActionIcon == null
                        || customActionIcon.sprite == null || !customActionIcon.gameObject.activeSelf
                        || customActionFloat.GetComponent<SortingGroup>() == null
                        || !Mathf.Approximately(customActionFloat.StackOffset.y, 0f)
                        || !Mathf.Approximately(stackFloats[1].StackOffset.y,
                            customActionPrefab.height + stackColors.Spacing.y)
                        || !Mathf.Approximately(stackFloats[0].StackOffset.y,
                            customActionPrefab.height + stackColors.Spacing.y
                            + normalDamagePrefab.damageUi.height + stackColors.Spacing.y))
                        throw new BuildFailedException("Same-target damage floats did not stack by official height and spacing.");
                    stackScene.Tick(0.06f);
                    if (customActionText.color.a < 0.9f
                        || customActionFloat.GetComponent<OfficialBattleHudAnimationPlayer>() == null)
                        throw new BuildFailedException("Official custom action world float animation did not reveal its text.");
                }
                finally
                {
                    stackScene?.Dispose();
                    UnityEngine.Object.DestroyImmediate(stackRoot);
                    UnityEngine.Object.DestroyImmediate(stackHud);
                }

                scene.Refresh(checkpoint);
                bool renderedMesh = false;
                foreach (MeshFilter filter in sceneRoot.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0) continue;
                    MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                    if (renderer == null || renderer.sharedMaterial == null || renderer.sharedMaterial.mainTexture == null)
                        throw new BuildFailedException("Native battle mesh has no material atlas texture.");
                    renderedMesh = true;
                }
                if (!renderedMesh)
                    throw new BuildFailedException("Native battle model 3207 did not create a textured mesh.");
                scene.Play(monster.Id, "skill_01");
                scene.Tick(1f / 30f);
            }
            finally
            {
                scene?.Dispose();
                models.Dispose();
                UnityEngine.Object.DestroyImmediate(sceneRoot);
                UnityEngine.Object.DestroyImmediate(hudParent);
            }
        }

        private static Transform FindPrefabPath(Transform prefabRoot, string path)
        {
            if (prefabRoot == null || string.IsNullOrEmpty(path)) return null;
            string[] parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            int index = parts.Length > 0 && parts[0] == prefabRoot.name ? 1 : 0;
            Transform current = prefabRoot;
            for (; index < parts.Length; index++)
            {
                current = current.Find(parts[index]);
                if (current == null) return null;
            }
            return current;
        }

        private static bool HasLocalizedMarkerText(Transform marker, string expected)
        {
            foreach (TMP_Text label in marker.GetComponentsInChildren<TMP_Text>(true))
                if (label.text == expected) return true;
            return false;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Game/Resources")) AssetDatabase.CreateFolder("Assets/Game", "Resources");
            if (!AssetDatabase.IsValidFolder(ResourcesRoot)) AssetDatabase.CreateFolder("Assets/Game/Resources", "Combat");
        }

        private static void CollectSpriteSettings(BattleHudNode node,
            System.Collections.Generic.Dictionary<string, BattleHudSprite> settings)
        {
            if (node == null) return;
            if (node.has_image_sprite && !string.IsNullOrEmpty(node.image_sprite_file))
            {
                var sprite = new BattleHudSprite
                {
                    file = node.image_sprite_file,
                    width = node.image_sprite_width,
                    height = node.image_sprite_height,
                    border = node.image_sprite_border,
                    pivot = node.image_sprite_pivot,
                    ppu = node.image_sprite_ppu
                };
                if (settings.TryGetValue(sprite.file, out BattleHudSprite existing))
                {
                    if (Conflicts(existing.border, sprite.border) || Conflicts(existing.pivot, sprite.pivot)
                        || existing.width != sprite.width || existing.height != sprite.height)
                        throw new BuildFailedException("Official HUD sprite metadata conflicts: " + sprite.file
                            + " at " + node.path + " (size " + existing.width + "x" + existing.height + "/"
                            + sprite.width + "x" + sprite.height + ", border " + Values(existing.border) + "/"
                            + Values(sprite.border) + ", pivot " + Values(existing.pivot) + "/" + Values(sprite.pivot) + ").");
                    if (existing.border == null) existing.border = sprite.border;
                    if (existing.pivot == null) existing.pivot = sprite.pivot;
                }
                else if (!string.IsNullOrEmpty(sprite.file)) settings.Add(sprite.file, sprite);
            }
            if (node.children != null)
                foreach (BattleHudNode child in node.children) CollectSpriteSettings(child, settings);
        }

        private static bool Same(float[] left, float[] right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++)
                if (!Mathf.Approximately(left[i], right[i])) return false;
            return true;
        }

        private static int Count(BattleHudNode node, Func<BattleHudNode, bool> predicate)
        {
            if (node == null) return 0;
            int count = predicate(node) ? 1 : 0;
            if (node.children != null)
                foreach (BattleHudNode child in node.children) count += Count(child, predicate);
            return count;
        }

        private static bool Conflicts(float[] left, float[] right)
        {
            return left != null && right != null && !Same(left, right);
        }

        private static string Values(float[] values)
        {
            return values == null ? "null" : string.Join(",", values);
        }

        private static string Sha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                var value = new StringBuilder(64);
                foreach (byte item in sha.ComputeHash(bytes)) value.Append(item.ToString("x2"));
                return value.ToString();
            }
        }
    }
}
