using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Presentation.CharacterCreation;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private sealed partial class OfficialMainlineHudModule
        {
            public Texture2D playerHeadTexture;

            // Original HeadCharacterPortraitBuilder settings, using the same
            // recovered native geometry/photography as the character and battle HUDs.
            private IEnumerator CapturePlayerHead(OfficialMap11UiData data)
            {
                var binding = data.components.Single(value => value.panel == "WorldCanvas" && value.type == "HeadCharacterPortrait").fields.mRequestor;
                var parameters = data.headPortraitSettings;
                var appearance = host.progression.ResolveAppearance();
                foreach (int type in parameters.mExcludeSkinTypes) appearance.SkinByType.Remove(type);
                GameObject model = CharacterCreationNativeModelFactory.CreatePreviewModel(host.slotId, host.persistentDataPath, appearance);
                try
                {
                    model.transform.position = new Vector3(-1024, -1024, -1024);
                    Animator animator = model.GetComponent<Animator>();
                    if (animator == null || !OfficialBattleCharacterAnimationCatalog.TryResolve(
                        animator.runtimeAnimatorController, binding.mAnimationName, out string state))
                        throw new InvalidOperationException("The original world head portrait pose has not been converted.");
                    var settings = new CharacterPortraitPhotography
                    {
                        animation = binding.mAnimationName,
                        width = parameters.TextureWidth, height = parameters.TextureHeight,
                        depth = parameters.mDepthBuffer, size = parameters.Size,
                        near = .3f, far = 1000f,
                        offset = new[] { parameters.mOffset.x, parameters.mOffset.y, parameters.mOffset.z },
                        excludes = parameters.mExcludeSkinTypes, captureParticles = parameters.mCaptureParticle != 0
                    };
                    playerHeadTexture = CharacterNativePortraitCapture.Capture(model, settings, state);
                    GameObject head = ui?.Find("WorldCanvas", binding.mRawImage);
                    if (head != null)
                    {
                        var image = head.GetComponent<RawImage>();
                        image.uvRect = data.components.Single(value => value.panel == "WorldCanvas"
                            && value.path == binding.mRawImage && value.type == "WindowMaskRawImage").fields.m_UVRect.Value();
                        image.texture = playerHeadTexture;
                        // CharacterPortraitRequestor.OnTextureUpdate preserves the native portrait size.
                        image.SetNativeSize();
                    }
                }
                finally { Destroy(model); }
                yield break;
            }
        }
    }
}
