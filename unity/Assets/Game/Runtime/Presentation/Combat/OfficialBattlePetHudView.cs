using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.Combat
{
    // GamePetSkillIcon.UpdateIcon/UpdateEnergy, using its original serialized
    // fields. Configuration/resource lookup is local; no layout is constructed.
    internal sealed class OfficialBattlePetHudView
    {
        [Serializable] private sealed class References
        {
            public string mPetIcon, mPetEnergyProgressImg, mPetEnergyProgressText;
            public string mPetSupportSkillIcon, mPetSupportSkillQuality;
            public string mNoEquipPetShowUI, mPetSupportSkillGo, mPetIconGo;
            public string mTriggerSupportSkillAnim, mDisabledGO;
        }
        [Serializable] private class SpriteBinding
        {
            public string resource;
            public Vector2 sourceSize, trimOffset, trimSize, uvMin, uvMax;
            [NonSerialized] public Sprite sprite;
        }
        [Serializable] private sealed class Evolution : SpriteBinding
        { public int classId, phase, supportSkillId; }
        [Serializable] private sealed class SupportIcon : SpriteBinding
        { public int itemId; }
        [Serializable] private sealed class Rank { public int rank, quality; }
        [Serializable] private sealed class DefaultRank { public int itemId, rank; }
        [Serializable] private sealed class Data
        {
            public string manifest;
            public References references;
            public Evolution[] evolutions;
            public SupportIcon[] supportIcons;
            public Rank[] ranks;
            public DefaultRank[] defaultRanks;
            public Rotation[] rotations;
            public ExtraCurve[] extraCurves;
        }
        [Serializable] private sealed class Rotation
        { public string path; public RotationKey[] keys; }
        [Serializable] private sealed class RotationKey
        {
            public float time;
            public Vector4 value, inSlope, outSlope, inWeight, outWeight;
            public int weightedMode;
        }
        [Serializable] private sealed class ExtraCurve
        { public string path, property; public FloatKey[] keys; }
        [Serializable] private sealed class FloatKey
        { public float time, value, inSlope, outSlope, inWeight, outWeight; public int weightedMode; }
        private sealed class NativeClipLifetime : MonoBehaviour
        {
            internal AnimationClip Clip;
            private void OnDestroy() { if (Clip) UnityEngine.Object.Destroy(Clip); }
        }
        private readonly Animation nativeAnimation;
        private const string Discharge = "FightMainPanel_GamePetSkillIconItem_Discharge";

        [Serializable] private sealed class Receipt { public string manifest, output_sha256; }
        private static Data loaded;
        private readonly Data data;
        private readonly Image petIcon, energy, supportIcon, supportQuality;
        private readonly TMP_Text energyText;
        private readonly GameObject empty, supportRoot, iconRoot, disabled;
        private int lastClassId = -1, lastPhase, lastRank;
        private readonly OfficialBattleHudAnimationPlayer animations;
        private int childhoodSupportSkill, currentSupportSkill;

        internal void PlaySkill(int itemId)
        {
            // Original GamePetSkillIcon.PlaySkill accepts either evolution's
            // support item. Play replaces this owner's playback from time zero.
            if (lastClassId <= 0 || itemId <= 0
                || itemId != childhoodSupportSkill && itemId != currentSupportSkill) return;
            if (!animations || !animations.Play(data.references.mTriggerSupportSkillAnim,
                Discharge))
                throw new InvalidOperationException("Original pet discharge animation is not bound.");
            nativeAnimation.Stop(); nativeAnimation.Play(Discharge); nativeAnimation.Sample();
        }

        internal OfficialBattlePetHudView(OfficialBattleHudRenderer renderer, OfficialBattleHudAnimationPlayer animations)
        {
            data = Load();
            this.animations = animations;
            GameObject Node(string path) => renderer.Find(path)
                ?? throw new InvalidOperationException("Original pet HUD field is missing: " + path);
            var refs = data.references;
            // The historical scalar HUD export omitted the clip's quaternion
            // channel. Let Unity sample the original legacy rotation curves. Scale Z uses the existing HUD clock.
            var animationOwner = Node(refs.mTriggerSupportSkillAnim);
            nativeAnimation = animationOwner.AddComponent<Animation>();
            nativeAnimation.playAutomatically = false;
            var clip = new AnimationClip { name = Discharge, legacy = true, wrapMode = WrapMode.ClampForever };
            foreach (var track in data.rotations)
                for (int axis = 0; axis < 4; axis++)
                {
                    var keys = track.keys.Select(key => new Keyframe(key.time, key.value[axis],
                        key.inSlope[axis], key.outSlope[axis], key.inWeight[axis], key.outWeight[axis])
                        { weightedMode = (WeightedMode)key.weightedMode }).ToArray();
                    clip.SetCurve(track.path, typeof(Transform), "localRotation." + "xyzw"[axis], new AnimationCurve(keys));
                }
            foreach (var track in data.extraCurves)
                animations.RestoreCurve(refs.mTriggerSupportSkillAnim, Discharge,
                    refs.mTriggerSupportSkillAnim + "/" + track.path, track.property.Replace("localScale.", "scale."),
                    new AnimationCurve(track.keys.Select(key => new Keyframe(key.time, key.value, key.inSlope,
                        key.outSlope, key.inWeight, key.outWeight) { weightedMode = (WeightedMode)key.weightedMode }).ToArray()));
            nativeAnimation.AddClip(clip, Discharge);
            animationOwner.AddComponent<NativeClipLifetime>().Clip = clip;
            petIcon = Node(refs.mPetIcon).GetComponent<Image>();
            energy = Node(refs.mPetEnergyProgressImg).GetComponent<Image>();
            energyText = Node(refs.mPetEnergyProgressText).GetComponent<TMP_Text>();
            supportIcon = Node(refs.mPetSupportSkillIcon).GetComponent<Image>();
            supportQuality = Node(refs.mPetSupportSkillQuality).GetComponent<Image>();
            empty = Node(refs.mNoEquipPetShowUI); supportRoot = Node(refs.mPetSupportSkillGo);
            iconRoot = Node(refs.mPetIconGo); disabled = Node(refs.mDisabledGO);
            if (!petIcon || !energy || !energyText || !supportIcon || !supportQuality)
                throw new InvalidOperationException("Original pet HUD component binding is incomplete.");
        }

        internal void Refresh(FightBattleCheckpoint checkpoint, OfflinePlayerProgress player)
        {
            var plans = player?.Skills?.Plans;
            int selection = player?.Skills?.SelectedPlan ?? -1;
            var plan = plans != null && selection >= 0 && selection < plans.Length ? plans[selection] : null;
            var pet = plan == null || plan.PetIndex <= 0 ? null
                : player.Pets?.Pets?.FirstOrDefault(value => value != null && value.Index == plan.PetIndex);
            bool equipped = pet != null && pet.ClassId > 0;
            // Original UpdateIcon visibility changes precede all configuration lookups.
            empty.SetActive(!equipped); supportRoot.SetActive(equipped); iconRoot.SetActive(equipped);
            if (!equipped) { lastClassId = 0; return; }
            var phase = data.evolutions.SingleOrDefault(value => value.classId == pet.ClassId && value.phase == pet.EvolutionPhase);
            var childhood = data.evolutions.SingleOrDefault(value => value.classId == pet.ClassId && value.phase == 1);
            if (phase == null || childhood == null)
                throw new InvalidOperationException("Original battle pet evolution is not bound: " + pet.ClassId + "/" + pet.EvolutionPhase);
            childhoodSupportSkill = childhood.supportSkillId;
            currentSupportSkill = phase.supportSkillId;
            int skill = childhood.supportSkillId;
            int rank = pet.SkillRanks?.FirstOrDefault(value => value != null && value.SkillId == skill)?.Rank
                ?? data.defaultRanks.Single(value => value.itemId == skill).rank;
            if (lastClassId != pet.ClassId || lastPhase != pet.EvolutionPhase || lastRank != rank)
            {
                ApplyImage(petIcon, phase);
                ApplyImage(supportIcon, data.supportIcons.Single(value => value.itemId == skill));
                OfficialBattleSkillQualityCatalog.Load().ApplyQuality(supportQuality,
                    data.ranks.Single(value => value.rank == rank).quality);
                lastClassId = pet.ClassId; lastPhase = pet.EvolutionPhase; lastRank = rank;
            }
            // The original provider reads the pet entity's current energy. A
            // missing transfer entity does not synthesize a percentage from time.
            FightBattlePetEnergyState state = checkpoint?.Units?.Select(value => value.PetEnergy)
                .FirstOrDefault(value => value != null && value.Enabled && value.OwnerUnitId == 1
                    && value.PetClassId == pet.ClassId);
            if (state != null)
            {
                float percent = Math.Abs(state.NeedEnergy) < double.Epsilon ? 0f : (float)(state.CurrentEnergy / state.NeedEnergy);
                energy.fillAmount = percent;
                energyText.text = WorldBattleReportNumberFormat.Percent(percent);
            }
            // The offline checkpoint has no original FightPetComponent.IsDisabled
            // projection yet. Preserve the prefab instead of inventing a rule.
        }

        private static void ApplyImage(Image image, SpriteBinding phase)
        {
            // pet_1619 remains hidden under the existing confirmed missing-art decision.
            image.enabled = !string.IsNullOrEmpty(phase.resource);
            if (!image.enabled) return;
            if (!phase.sprite)
            {
                var texture = Resources.Load<Texture2D>(phase.resource)
                    ?? throw new InvalidOperationException("Original pet portrait is missing: " + phase.resource);
                phase.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100);
            }
            image.sprite = phase.sprite;
            var geometry = image.GetComponent<OfficialBattleSkillQualityGeometry>()
                ?? image.gameObject.AddComponent<OfficialBattleSkillQualityGeometry>();
            geometry.Bind(new BattleSkillQualityBinding { sprite = phase.sprite, sourceSize = phase.sourceSize,
                trimOffset = phase.trimOffset, trimSize = phase.trimSize, uvMin = phase.uvMin, uvMax = phase.uvMax });
        }

        private static Data Load()
        {
            if (loaded != null) return loaded;
            var text = Resources.Load<TextAsset>("Combat/battle-pet-hud");
            var proof = Resources.Load<TextAsset>("Combat/battle-pet-hud-source-manifest");
            if (!text || !proof) throw new InvalidOperationException("Original battle pet HUD data is missing.");
            var receipt = JsonUtility.FromJson<Receipt>(proof.text);
            string hash;
            using (var sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.text))).Replace("-", "").ToLowerInvariant();
            var result = JsonUtility.FromJson<Data>(text.text);
            if (receipt.manifest != "854_156383" || receipt.output_sha256 != hash || result.manifest != receipt.manifest
                || result.references == null || result.evolutions == null || result.supportIcons == null
                || result.ranks == null || result.defaultRanks == null || result.rotations == null || result.extraCurves == null)
                throw new InvalidOperationException("Original battle pet HUD source differs.");
            loaded = result;
            return result;
        }
    }
}
