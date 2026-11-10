using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Character;
using Zjcs.Offline.PartnersUI;
using Zjcs.Official.PartnersUI;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Presentation.Home
{
    public sealed class DailyDungeonResultPlayerRowAdapter : MonoBehaviour
    {
        [Serializable] private sealed class ChestCatalog
        { public ChestEntry[] chests; public GradeEntry[] grades; public AwardRow row; }
        [Serializable] private sealed class ChestEntry { public int chestId; public string resource; }
        [Serializable] private sealed class GradeEntry { public int grade, chestId; }
        [Serializable] private sealed class AwardRow
        { public string awardRoot; public float showInterval, defaultSlotScale; public int scrollMinCount; }
        private ChestCatalog chestCatalog;
        private PartnerNativeUiActor chest;
        private SkeletonGraphicAnimSequence chestSequence;
        private DailyDungeonUiBindingModule bindings;
        private HomeFeatureRuntime runtime;
        private IReadOnlyList<DailyDungeonClaimRewardDisplayItem> chestRewards;
        [Serializable] private sealed class PlayableCatalog
        { public ChestEffect[] grades; public QualityEffect[] qualities; public string openChest; }
        [Serializable] private sealed class ChestEffect { public int chestId; public string effect; }
        [Serializable] private sealed class QualityEffect { public int quality; public string open, loopEquip, loopPiece; }
        private PlayableCatalog playables;
        private OfflineBattleNativePlayableScope effects;
        private int chestId;
        public bool ChestOpened { get; private set; }

        public void ConfigureSettlement(int grade, DailyDungeonUiBindingModule uiBindings, HomeFeatureRuntime host)
        {
            runtime = host;
            bindings = uiBindings;
            TextAsset catalog = Resources.Load<TextAsset>("Dungeons/UI/884_156692/Native/Chests/catalog");
            chestCatalog = JsonUtility.FromJson<ChestCatalog>(catalog.text);
            int id = chestCatalog.grades.Single(value => value.grade == grade).chestId;
            chestId = id;
            playables = JsonUtility.FromJson<PlayableCatalog>(Resources.Load<TextAsset>(
                "Dungeons/UI/884_156692/Native/Playables/catalog").text);
            var sound = gameObject.AddComponent<OfflineProfessionSoundHost>();
            sound.Bind(Resources.Load<OfflineProfessionAudioCatalog>(
                "Dungeons/UI/884_156692/Native/Playables/audio-catalog"));
            if (!FindObjectOfType<AudioListener>()) gameObject.AddComponent<AudioListener>();
            effects = new OfflineBattleNativePlayableScope(transform, sound);
            chest = GetComponentInChildren<PartnerNativeUiActor>(true);
            if (!chest) throw new InvalidOperationException("The original result chest native binding is missing.");
            var asset = Resources.Load<PartnerNativeUiSkeletonAsset>(
                chestCatalog.chests.Single(value => value.chestId == id).resource);
            chest.SelectAsset(asset);
            chestSequence = chest.GetComponent<SkeletonGraphicAnimSequence>();
            chestSequence.Configure(component => chest);
            chestSequence.SetSkeletonGraphic(chest.Graphic);
            chestSequence.OnAnimStart = name =>
            {
                if (name != "open_01") return;
                ChestOpened = true;
                PlayEffect(playables.grades.Single(value => value.chestId == chestId).effect, chest.gameObject);
                StartCoroutine(RevealChestRewards());
            };
        }

        public void OpenRewards(IReadOnlyList<DailyDungeonClaimRewardDisplayItem> rewards)
        {
            chestRewards = rewards;
            chestSequence.Play();
            PlayEffect(playables.openChest, gameObject);
        }

        private void PlayEffect(string path, GameObject target)
        {
            if (!string.IsNullOrEmpty(path)) effects.Play(Resources.Load<GameObject>(path), target, target);
        }
        private void Update() => effects?.Tick(Time.deltaTime);
        private void LateUpdate() => effects?.LateTick();

        private IEnumerator RevealChestRewards()
        {
            Transform root = DailyDungeonUiBindingModule.FindNode(transform, chestCatalog.row.awardRoot, null);
            var prefab = Resources.Load<GameObject>(
                "Dungeons/UI/884_156692/Native/Prefabs/UI/DailyDungeon/DailyDungeonEndPaneAwardlItem");
            var iconPrefab = Resources.Load<GameObject>("Dungeons/UI/884_156692/Native/Prefabs/UI/Item/ItemIcon");
            var awards = new List<DailyDungeonResultAwardSlotAdapter>();
            for (int i = 0; i < (chestRewards?.Count ?? 0); i++)
            {
                var reward = chestRewards[i];
                GameObject slot = Instantiate(prefab, root, false);
                bindings.Apply(slot, "DailyDungeonEndPaneAwardlItem");
                slot.name = "DailyDungeonChestAward_" + reward.ItemId;
                slot.transform.localScale = Vector3.one * chestCatalog.row.defaultSlotScale;
                Transform anchor = slot.transform.Find("Content/ItemGo");
                GameObject icon = Instantiate(iconPrefab, anchor, false);
                bindings.Apply(icon, "ItemIcon");
                icon.AddComponent<DailyDungeonResultItemIconAdapter>().Configure(runtime, reward.ItemId, reward.Count);
                var award = slot.AddComponent<DailyDungeonResultAwardSlotAdapter>();
                slot.transform.Find("Content").gameObject.SetActive(false);
                var info = DailyDungeonRewardAssetCatalog.LoadDefault().Find(reward.ItemId);
                QualityEffect quality = playables.qualities.FirstOrDefault(value => value.quality == info.quality);
                if (quality != null)
                {
                    award.PlayOpenEffect = () => PlayEffect(quality.open, icon);
                    Transform loop = slot.GetComponentsInChildren<Transform>(true)
                        .Single(value => value.name == "QualityEffectGo");
                    PlayEffect(info.isEquipment ? quality.loopEquip
                        : info.isCommonEquipmentPiece ? quality.loopPiece : null, loop.gameObject);
                }
                awards.Add(award);
            }
            var scroll = GetComponentInChildren<DailyDungeonAwardAutoScroll>(true);
            if (!scroll) throw new InvalidOperationException("The original daily award auto scroll is unbound.");
            if (awards.Count <= chestCatalog.row.scrollMinCount) scroll.LockDrag();
            bool scrollStarted = false;
            for (int i = 0; i < awards.Count; i++)
            {
                awards[i].ShowAward();
                if (!scrollStarted && i + 1 > chestCatalog.row.scrollMinCount)
                {
                    scrollStarted = true;
                    var layout = root.GetComponent<HorizontalOrVerticalLayoutGroup>();
                    float spacing = layout ? layout.spacing : 0;
                    scroll.Play(scroll.CalcDurationByStep(((RectTransform)awards[i].transform).rect.height
                        + spacing, chestCatalog.row.showInterval));
                }
                if (chestCatalog.row.showInterval > 0 && i + 1 < awards.Count)
                    yield return new WaitForSeconds(chestCatalog.row.showInterval);
            }
        }

        private const string DefaultSpineMaterialPath =
            "Dungeons/UI/884_156692/Materials/Spine/SkeletonGraphicDefault";
        private const string DyeSpineMaterialPath =
            "Dungeons/UI/884_156692/Materials/Spine/SkeletonGraphicDye";
        private readonly Dictionary<Graphic, Material> originalSpineMaterials =
            new Dictionary<Graphic, Material>();
        private readonly Dictionary<Graphic, Material> runtimeSpineMaterials =
            new Dictionary<Graphic, Material>();
        public void Configure(HomeFeatureRuntime host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            BindRuntimeSpineMaterials();

            TMP_Text name = FindNamedComponent<TMP_Text>("PlayerName");
            if (name != null)
                name.text = string.IsNullOrWhiteSpace(host.CharacterName) ? "本地玩家" : host.CharacterName;

            var portrait = OfficialBattleCharacterPortraits.ForProfile(gameObject,
                host.DailyDungeonProgression, host.ActiveProfileSlotId, host.SavesRoot);
            Sprite playerPortrait = portrait(new FightBattleUnitState { Id = 1, CharacterType = 1 });
            if (playerPortrait == null)
                throw new InvalidOperationException("Official daily dungeon player portrait is unavailable.");

            bool bound = false;
            foreach (Image image in GetComponentsInChildren<Image>(true))
            {
                if (!string.Equals(image.gameObject.name, "HeadImg", StringComparison.Ordinal)) continue;
                image.sprite = playerPortrait;
                image.color = Color.white;
                image.preserveAspect = true;
                bound = true;
            }
            foreach (RawImage image in GetComponentsInChildren<RawImage>(true))
            {
                if (!string.Equals(image.gameObject.name, "HeadImg", StringComparison.Ordinal)) continue;
                image.texture = playerPortrait.texture;
                image.color = Color.white;
                bound = true;
            }
            if (!bound)
            {
                throw new InvalidOperationException("Official daily dungeon player portrait slot is missing.");
            }

            Transform thumbsUp = FindNamedTransform("FightThumbsUpUI");
            if (thumbsUp != null) thumbsUp.gameObject.SetActive(false);
            foreach (Button button in GetComponentsInChildren<Button>(true))
                button.interactable = false;
        }

        private void BindRuntimeSpineMaterials()
        {
            Material defaultTemplate = Resources.Load<Material>(DefaultSpineMaterialPath);
            Material dyeTemplate = Resources.Load<Material>(DyeSpineMaterialPath);
            if (defaultTemplate == null || defaultTemplate.shader == null
                || dyeTemplate == null || dyeTemplate.shader == null)
                throw new InvalidOperationException("Official daily dungeon Windows Spine materials are missing.");

            int boundCount = 0;
            foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
            {
                if (runtimeSpineMaterials.TryGetValue(graphic, out Material existing))
                {
                    graphic.material = existing;
                    boundCount++;
                    continue;
                }

                Material source = graphic.material;
                if (source == null || source.shader == null) continue;
                Material template = source.shader.name == "Spine/SkeletonGraphicDye"
                    ? dyeTemplate : source.shader.name == "Spine/SkeletonGraphic" ? defaultTemplate : null;
                if (template == null) continue;
                if (!template.shader.isSupported)
                    throw new InvalidOperationException("Daily dungeon Spine shader is unsupported: "
                        + template.shader.name);
                Material runtimeMaterial = new Material(source)
                {
                    name = source.name + "_WindowsRuntime",
                    hideFlags = HideFlags.DontSave,
                    shader = template.shader,
                    renderQueue = source.renderQueue
                };
                originalSpineMaterials.Add(graphic, source);
                runtimeSpineMaterials.Add(graphic, runtimeMaterial);
                graphic.material = runtimeMaterial;
                graphic.SetMaterialDirty();
                boundCount++;
            }
            if (boundCount == 0)
                throw new InvalidOperationException("Official daily dungeon Spine atlas materials are missing.");
        }

        private void OnDestroy()
        {
            effects?.Dispose();
            effects = null;
            foreach (KeyValuePair<Graphic, Material> pair in runtimeSpineMaterials)
            {
                if (pair.Key != null && pair.Key.material == pair.Value)
                    pair.Key.material = originalSpineMaterials[pair.Key];
                if (pair.Value != null) Destroy(pair.Value);
            }
            runtimeSpineMaterials.Clear();
            originalSpineMaterials.Clear();
        }

        private T FindNamedComponent<T>(string nodeName) where T : Component
        {
            foreach (T component in GetComponentsInChildren<T>(true))
                if (component != null && string.Equals(component.gameObject.name, nodeName, StringComparison.Ordinal))
                    return component;
            return null;
        }

        private Transform FindNamedTransform(string nodeName)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
                if (child != null && string.Equals(child.name, nodeName, StringComparison.Ordinal)) return child;
            return null;
        }
    }
}
