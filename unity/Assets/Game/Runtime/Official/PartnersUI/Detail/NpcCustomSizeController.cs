// Migrated original detail presentation; see partner-detail-talk-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.WorldUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.StoryBubbles;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using Object = UnityEngine.Object;
using System;
using UnityEngine;

public class NpcCustomSizeController : MonoBehaviour
{
    [Serializable]
    public struct NpcSizeConfig
    {
        public Vector2 Offset;

        public Vector3 Scale;
    }

    [SerializeField]
    private RectTransform mSizeTarget;

    [SerializeField]
    private NpcSizeConfig mDefaultSizeConfig;

    [SerializeField]
    private MonoDictionary<int, NpcSizeConfig> mNpcConfigDict;

    public bool TryGetConfig(int npcId, out NpcSizeConfig sizeConfig)
    {
        if (mNpcConfigDict != null && mNpcConfigDict.TryGetValue(npcId, out var value))
        {
            sizeConfig = value;
            return true;
        }
        sizeConfig = mDefaultSizeConfig;
        return false;
    }

    public void ApplyForNpc(int npcId)
    {
        if (!((Object)(object)mSizeTarget == (Object)null))
        {
            TryGetConfig(npcId, out var sizeConfig);
            mSizeTarget.anchoredPosition = sizeConfig.Offset;
            ((Transform)mSizeTarget).localScale = sizeConfig.Scale;
        }
    }
}

}
