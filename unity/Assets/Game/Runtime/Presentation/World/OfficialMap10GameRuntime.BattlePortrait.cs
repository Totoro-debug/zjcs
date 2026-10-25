using System;
using UnityEngine;
using Zjcs.Presentation.CharacterCreation;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMap10GameRuntime
    {
        private Sprite CaptureBattlePlayerHead(GameObject owner)
        {
            var settings = CharacterPortraitPhotography.LoadBattleHead();
            var appearance = progression.ResolveAppearance();
            foreach (int type in settings.excludes) appearance.SkinByType.Remove(type);
            GameObject model = CharacterCreationNativeModelFactory.CreatePreviewModel(slotId, persistentDataPath, appearance);
            try
            {
                model.transform.position = new Vector3(-1024, -1024, -1024);
                Animator animator = model.GetComponent<Animator>();
                if (!OfficialBattleCharacterAnimationCatalog.TryResolve(animator.runtimeAnimatorController, settings.animation, out string state))
                    throw new InvalidOperationException("The original battle head photography pose has not been converted.");
                Texture2D texture = CharacterNativePortraitCapture.Capture(model, settings, state);
                var lease = owner.AddComponent<CharacterNativePortraitLease>();
                lease.Initialize(texture);
                return lease.Sprite;
            }
            finally { Destroy(model); }
        }
    }
}
