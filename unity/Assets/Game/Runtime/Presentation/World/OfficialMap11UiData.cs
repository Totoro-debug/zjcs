using System;

namespace Zjcs.Presentation.World
{
    [Serializable]
    public sealed class OfficialMap11UiData
    {
        public string manifest;
        public int mapClassId;
        public OfficialMap10UiPanel[] panels;
        public OfficialMap10UiSpriteRecord[] sprites;
        public OfficialMap11UiBindings[] bindings;
        public OfficialMap11UiIconBinding[] iconBindings;
        public OfficialMap11WorldBindings worldBindings;
        public OfficialMap11UiComponent[] components;
        public OfficialMap11UiText[] texts;
        public OfficialMap11TmpSpriteAsset commonSpriteAsset;
        public OfficialMap11ItemIcon[] itemIcons;
        public string itemQualitySprite;
        public float[] enoughColor, lackColor;
    }

    [Serializable] public sealed class OfficialMap11ItemIcon { public int itemId; public string sprite; }
    [Serializable] public sealed class OfficialMap11TmpSpriteAsset
    {
        public string name, version, texturePath, textureSha256;
        public int hashCode;
        public OfficialMap11TmpGlyph[] glyphs;
        public OfficialMap11TmpCharacter[] characters;
    }
    [Serializable] public sealed class OfficialMap11TmpGlyph
    {
        public uint index;
        public float[] metrics;
        public int[] rect;
        public float scale;
        public int atlasIndex;
    }
    [Serializable] public sealed class OfficialMap11TmpCharacter
    {
        public string name;
        public int hashCode;
        public uint unicode, glyphIndex;
        public float scale;
    }

    [Serializable] public sealed class OfficialMap11UiComponent
    {
        public string panel, path, type;
        public OfficialMap11UiComponentFields fields;
    }

    [Serializable] public sealed class OfficialMap11UiComponentFields
    {
        public OfficialMap11UiMaterial material;
        public OfficialMap11UiClip[] clips;
        public string TitleText, ContentText, HintGo, HintText, ConfirmBtn, CancelBtn, ConfirmText, CancelText, LockMaskWithTime;
        public string CostMoneyContent, MoneyContent, CostItemIcon, CostText, ItemContentObj, Slot, mSlot;
        public string QualityImg, QualityShadowImg, myText, myTextGo, myContent, BottomGo, myBottomText, ItemIconAttachment, mFakeIcon, mProgress, BgTransform;
        public string FlareStarItemIcon, DiamondItemIcon, ExchangeFixedTxt, CountSliderGo;
        public int mirrorType;
        public bool reversed;
        public OfficialMap11IconProxy mIconProxy;
        public UnityEngine.Color EnoughColor, LackColor;
        public int m_Enabled;
        public string Text;
        public int mNumberSize;
        public int m_StartCorner, m_StartAxis, m_Constraint, m_ConstraintCount, m_ChildAlignment;
        public OfficialMap11Padding m_Padding;
        public UnityEngine.Vector2 m_CellSize, m_Spacing;
        public float spacing;
        public float[] m_SpacingList;
        public int m_ChildForceExpandWidth, m_ChildForceExpandHeight, m_ChildControlWidth, m_ChildControlHeight;
        public int m_ChildScaleWidth, m_ChildScaleHeight, m_ReverseArrangement;
    }
    [Serializable] public sealed class OfficialMap11IconProxy { public string mImage; }
    [Serializable] public sealed class OfficialMap11Padding { public int m_Left, m_Right, m_Top, m_Bottom; }

    [Serializable] public sealed class OfficialMap11UiMaterial { public string name, shader; }
    [Serializable] public sealed class OfficialMap11UiClip
    {
        public string field, name;
        public float duration;
        public bool loop;
        public OfficialMap11UiTrack[] tracks;
        public string[] unknown;
    }
    [Serializable] public sealed class OfficialMap11UiTrack
    {
        public string path, property;
        // JsonUtility does not support nested arrays; the converter also emits named key fields.
        public OfficialMap11UiKey[] keyframes;
    }
    [Serializable] public sealed class OfficialMap11UiKey { public float time, value, inTangent, outTangent; }

    [Serializable] public sealed class OfficialMap11WorldBindings
    {
        public string[] withoutCharacter, withCharacter, leader, member, hideUi;
        public string[] idleMatchContents, downloadContents;
        public OfficialMap11SafeAreaBinding[] safeAreas;
        public OfficialMap11CanvasScaler canvasScaler;
        public OfficialMap11ExploreButtonBinding[] exploreButtons;
        public string[] chatActionExpanded, quickMessageMasks, actionEndButtons;
        public OfficialMap11TeamPanelBinding[] teamPanels;
    }

    [Serializable] public sealed class OfficialMap11ExploreButtonBinding
    {
        public string path;
        public bool isOneTime;
        public OfficialMap11UiState[] states;
    }

    [Serializable] public sealed class OfficialMap11TeamPanelBinding
    {
        public string path;
        public OfficialMap11UiState[] states;
    }

    [Serializable] public sealed class OfficialMap11SafeAreaBinding
    {
        public string path;
        public float paddingTop, paddingBottom;
        public bool enabled;
    }

    [Serializable] public sealed class OfficialMap11CanvasScaler
    {
        public int scaleMode, screenMatchMode;
        public float referencePixelsPerUnit, scaleFactor, matchWidthOrHeight;
        public float[] referenceResolution;
    }

    [Serializable]
    public sealed class OfficialMap11UiText
    {
        public string key;
        public string value;
    }

    [Serializable]
    public sealed class OfficialMap11UiBindings
    {
        public string key, touchButton, cannotButton, transferButton, title, conditionText;
        public string propTemplate, propParent, blessingText, treasureParent, iconSlot, iconKey;
        public OfficialMap11UiState[] states, conditionStates;
    }

    [Serializable]
    public sealed class OfficialMap11UiIconBinding
    {
        public string key, path;
        public OfficialMap11UiState[] states;
    }

    [Serializable]
    public sealed class OfficialMap11UiState
    {
        public int state;
        public string[] paths;
    }
}
