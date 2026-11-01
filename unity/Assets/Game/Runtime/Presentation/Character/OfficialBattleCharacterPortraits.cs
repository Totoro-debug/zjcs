using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Presentation.CharacterCreation;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Skills;

namespace Zjcs.Presentation.Character
{
    /// <summary>Original battle head photography, shared by all local battle entry points.
    /// The battle owner retains the captured sprites until its scene timeline is released.</summary>
    public static class OfficialBattleCharacterPortraits
    {
        // All original male/female atlas materials serialize this value; see
        // assets/reference/battle-hud/character-portrait-material-source-manifest.json.
        private const float OriginalWorldClipHeight = -1000000f;
        private static readonly int WorldClipHeight = Shader.PropertyToID("_WorldClipHeight");

        public static Func<FightBattleUnitState, Sprite> ForProfile(GameObject owner,
            OfflineCharacterProgressionService progression, string slotId, string savesRoot,
            Func<FightBattleUnitState, int[], GameObject> otherCharacter = null)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (progression == null) throw new ArgumentNullException(nameof(progression));
            if (string.IsNullOrWhiteSpace(slotId)) throw new ArgumentException("A portrait profile is required.", nameof(slotId));
            var captured = new Dictionary<int, Sprite>();
            CharacterPortraitPhotography settings = null;
            return unit =>
            {
                if (unit == null || unit.NpcClassId > 0 || unit.Id != 1 && unit.CharacterType != 1) return null;
                if (owner == null) throw new ObjectDisposedException("Battle portrait owner");
                if (captured.TryGetValue(unit.Id, out Sprite existing)) return existing;
                settings ??= CharacterPortraitPhotography.LoadBattleHead();
                GameObject model;
                if (unit.Id == 1)
                {
                    var appearance = progression.ResolveAppearance();
                    foreach (int type in settings.excludes) appearance.SkinByType.Remove(type);
                    model = CharacterCreationNativeModelFactory.CreatePreviewModel(slotId, savesRoot, appearance);
                }
                else model = otherCharacter?.Invoke(unit, settings.excludes);
                if (model == null) return null;
                Texture2D texture = null;
                try
                {
                    model.transform.position = new Vector3(-1024, -1024, -1024);
                    OfficialSkillPreviewMaterials.Bind(model, OfficialSkillAssetCatalog.LoadDefault().NativePortraitShader);
                    var properties = new MaterialPropertyBlock();
                    foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                    {
                        renderer.GetPropertyBlock(properties);
                        properties.SetFloat(WorldClipHeight, OriginalWorldClipHeight);
                        renderer.SetPropertyBlock(properties);
                        properties.Clear();
                    }
                    Animator animator = model.GetComponent<Animator>();
                    if (animator == null || !OfficialBattleCharacterAnimationCatalog.TryResolve(
                        animator.runtimeAnimatorController, settings.animation, out string state))
                        throw new InvalidOperationException("The original battle head photography pose has not been converted.");
                    texture = CharacterNativePortraitCapture.Capture(model, settings, state);
                    var lease = owner.AddComponent<CharacterNativePortraitLease>();
                    lease.Initialize(texture);
                    texture = null;
                    captured.Add(unit.Id, lease.Sprite);
                    return lease.Sprite;
                }
                finally
                {
                    if (texture != null) UnityEngine.Object.Destroy(texture);
                    UnityEngine.Object.Destroy(model);
                }
            };
        }
    }
}
