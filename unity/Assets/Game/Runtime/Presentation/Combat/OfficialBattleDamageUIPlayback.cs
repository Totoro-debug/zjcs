using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleDamageUI;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.WorldUI;
using CharacterDamageUIManager = Zjcs.Official.BattleDamageUI.CharacterDamageUIManager;
using ElementConfrontationUI = Zjcs.Official.BattleDamageUI.ElementConfrontationUI;

namespace Zjcs.Presentation.Combat
{
    // Resource and notification adapter for the original manager. Styling,
    // stacking, filtering, animation and pool policy remain in the original UI.
    internal sealed class OfficialBattleDamageUIPlayback : CharacterDamageUIContext, IDisposable
    {
        [Serializable] private sealed class Bindings { public Binding[] prefabs; }
        [Serializable] private sealed class Binding { public string guid, resource; }
        [Serializable] private sealed class PoolSettings { public bool ECIsUseGameObjectPool; }
        [Serializable] private sealed class Language { public TextEntry[] texts; }
        [Serializable] private sealed class TextEntry { public string key, text; }
        private readonly OfficialBattleSkillEntityPlayback views;
        private readonly GameObject root;
        private readonly CharacterDamageUIManager manager;
        private readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        private readonly Dictionary<EcEntityIdentifier, CharacterDamageUIRole> roles =
            new Dictionary<EcEntityIdentifier, CharacterDamageUIRole>();
        private readonly Dictionary<EcEntityIdentifier, int> replayClasses = new Dictionary<EcEntityIdentifier, int>();
        private readonly Dictionary<Renderer, bool> hiddenRenderers = new Dictionary<Renderer, bool>();
        private bool visible = true;
        private readonly Dictionary<string, string> texts = new Dictionary<string, string>();
        private readonly Zjcs.Offline.ProfessionUI.OfflineUiFontInstance languageFont;
        public override Transform DamageFloatsRoot => root.transform;
        public override FightDamageSettings Settings { get; }
        public override bool UseGameObjectPool { get; }
        public override int? StageType { get; }
        public override CharacterDamageUIWarmupSettings WarmupSettings { get; }
        public override IEnumerable<CharacterDamageUIRole> Characters => roles.Values;

        public OfficialBattleDamageUIPlayback(OfficialBattleSkillEntityPlayback views, Transform parent, int stageType)
        {
            this.views = views ?? throw new ArgumentNullException(nameof(views));
            if (!parent) throw new ArgumentNullException(nameof(parent));
            StageType = stageType;
            Settings = Resources.Load<FightDamageSettings>("Combat/DamageUI/FightDamageSettings");
            var bindings = Resources.Load<TextAsset>("Combat/DamageUI/prefab-bindings");
            var warmup = Resources.Load<TextAsset>("Combat/DamageUI/warmup-settings");
            var poolSettings = Resources.Load<TextAsset>("Combat/hit-native-settings");
            if (!Settings || !bindings || !warmup || !poolSettings)
                throw new InvalidOperationException("Original battle damage UI configuration is unavailable.");
            UseGameObjectPool = JsonUtility.FromJson<PoolSettings>(poolSettings.text).ECIsUseGameObjectPool;
            WarmupSettings = JsonUtility.FromJson<CharacterDamageUIWarmupSettings>(warmup.text);
            var language = Resources.Load<TextAsset>("Combat/DamageUI/texts");
            var sourceFont = Resources.Load<TMPro.TMP_FontAsset>("Combat/DamageUI/LanguageFont");
            if (!language || !sourceFont) throw new InvalidOperationException("Original damage UI language assets are unavailable.");
            foreach (var entry in JsonUtility.FromJson<Language>(language.text).texts) texts.Add(entry.key, entry.text);
            foreach (var binding in JsonUtility.FromJson<Bindings>(bindings.text).prefabs)
            {
                var prefab = Resources.Load<GameObject>(binding.resource);
                if (!prefab) throw new InvalidOperationException("Original damage UI prefab is unavailable: " + binding.resource);
                prefabs.Add(binding.guid, prefab);
            }
            var rootPrefab = Resources.Load<GameObject>("Combat/DamageUIRoot");
            if (!rootPrefab) throw new InvalidOperationException("Original FightRoot damage-float subtree is unavailable.");
            root = UnityEngine.Object.Instantiate(rootPrefab, parent, false);
            try
            {
                languageFont = new Zjcs.Offline.ProfessionUI.OfflineUiFontInstance(sourceFont);
                // TMP can populate its font cache during Awake, before the
                // original pool returns the view for instance injection.
                var templates = new GameObject("DamageUIPrefabInstances");
                templates.SetActive(false);
                templates.transform.SetParent(root.transform, false);
                foreach (var key in new List<string>(prefabs.Keys))
                {
                    var template = UnityEngine.Object.Instantiate(prefabs[key], templates.transform, false);
                    foreach (var text in template.GetComponentsInChildren<TMPro.TMP_Text>(true))
                        if (text.font == sourceFont) text.font = languageFont.Font;
                    prefabs[key] = template;
                }
                manager = root.AddComponent<CharacterDamageUIManager>();
                manager.Context = this;
                manager.Init();
            }
            catch { Destroy(root); languageFont?.Dispose(); throw; }
        }

        public override MyEntity GetEntity(EcEntityIdentifier id) => views.GetEntity(id);
        public override GameObject LoadPrefab(string assetGuid) => prefabs[assetGuid];
        // Resources owns the shared assets; release this session's references.
        public override void ReleasePrefabs() => prefabs.Clear();
        public override void GetSkillPrefabAsync(EcEntityIdentifier id, Action<MyFightSkill> ready)
            => ready(replayClasses.TryGetValue(id, out var classId) ? views.GetSkillPrefab(classId) : null);
        // Confirmed offline assignment: both original element fields consume
        // the actual skill element already frozen by the executor for this hit.
        public override ElementType GetSkillElementType(MyFightDamage damage)
            => damage.Component.DamageResult.SkillElementType;
        public override void PrepareUI(CharacterDamageUI ui)
        {
            foreach (var text in ui.GetComponentsInChildren<WorldMultiLanguageText>(true))
            {
                text.Localizer = key => texts.TryGetValue(key, out var value) ? value : key;
                text.FontProvider = () => languageFont.Font;
                text.Refresh();
            }
            foreach (var element in ui.GetComponentsInChildren<ElementConfrontationUI>(true))
                element.ElementConfrontThreshold = WarmupSettings.ElementConfrontThreshold;
        }

        public void Register(MyEntity entity)
        {
            var fight = entity.GetComponent<MyFightRoleFight>();
            if (!fight) return;
            if (roles.TryGetValue(entity.Id, out var previous))
            {
                if (previous.Fight == fight) return;
                CharacterRemoved(previous);
            }
            var role = new CharacterDamageUIRole(fight);
            roles[entity.Id] = role;
            CharacterAdded(role);
        }
        public void Remove(MyEntity entity)
        {
            if (entity && roles.TryGetValue(entity.Id, out var role))
            { CharacterRemoved(role); roles.Remove(entity.Id); }
        }
        public void Replay(FightBattleSkillCastEvent cast, EcEntityIdentifier skillId)
        {
            if (!cast.IsReplay) return;
            replayClasses[skillId] = cast.SkillEntityId;
            SkillReplayed(new CharacterDamageUISkillReplay(new EcEntityIdentifier(1, cast.ActorId),
                skillId, cast.ReplayType == 1));
        }
        public void SkillCostHp(FightBattleSkillCastEvent cast, EcEntityIdentifier skillId)
        {
            if (cast.HasSkillCostHp && roles.TryGetValue(new EcEntityIdentifier(1, cast.ActorId), out var role))
                role.SkillCostHp(skillId, cast.SkillCostBeforeHp, cast.SkillCostAfterHp);
        }
        public bool StatusCustomAction(FightBattleStatusCustomActionEvent action)
        {
            if (action == null || action.Action < (int)EStatusCustomAction.Immunity
                || action.Action > (int)EStatusCustomAction.CDModify
                || !roles.TryGetValue(new EcEntityIdentifier(1, action.UnitId), out var role)
                || !role.Fight) return false;
            role.StatusTriggerCustomAction((EStatusCustomAction)action.Action);
            return true;
        }
        public void SetVisible(bool value)
        {
            visible = value;
            UpdateVisibility();
        }
        public void UpdateVisibility()
        {
            // Hiding a window must not disable a borrowed CharacterDamageUI:
            // its original OnDisable clears identity and stops its return timer.
            if (visible)
            {
                foreach (var pair in hiddenRenderers) if (pair.Key) pair.Key.forceRenderingOff = pair.Value;
                hiddenRenderers.Clear();
            }
            else foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!hiddenRenderers.ContainsKey(renderer)) hiddenRenderers.Add(renderer, renderer.forceRenderingOff);
                renderer.forceRenderingOff = true;
            }
        }
        public void Dispose()
        {
            if (!root) return;
            root.SetActive(false); // Original OnDisable detaches all role events before their views disappear.
            manager.Release();
            roles.Clear(); replayClasses.Clear();
            Destroy(root);
            languageFont.Dispose();
        }
        private static void Destroy(GameObject value)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
