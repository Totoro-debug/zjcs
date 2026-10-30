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
        public OfficialMap11GuidPrefab[] guidPrefabs;
        public string itemQualitySprite;
        public string mapBackgroundSprite;
        public OfficialMap11MiniMapSettings minimapSettings;
        public OfficialMap11MiniMapTab[] minimapTabs;
        public OfficialMap11PortraitSettings headPortraitSettings;
        public OfficialMap11TopBarItem[] topBarItems;
        public OfficialMap11SceneNode[] minimapCharacter;
        public OfficialMap11MiniMapMark[] minimapMarks;
        public OfficialMap11MiniMapPrefab[] minimapMarkPrefabs;
        public OfficialMap11MiniMapBinding[] minimapMarkBindings;
        public float[] enoughColor, lackColor;
        public float[] itemProgressEnoughColor, itemProgressLackColor;
        public int recommendCombatRatingIndex, suppressUnlockRank;
        public OfficialMap11RatingColors[] combatRatingColors;
        public OfficialExploreUiAssets explorationUi;
        public OfficialExploreQualityText[] qualityTextColors;
    }
    [Serializable] public sealed class OfficialExploreUiAssets { public OfficialExploreUiIcon[] icons; public OfficialExploreUiQuality[] qualities; public OfficialExploreUiRule[] rule; }
    [Serializable] public sealed class OfficialExploreUiIcon { public string name, sprite; }
    [Serializable] public sealed class OfficialExploreUiQuality { public int quality; public string sprite, detailSprite, awardLight; }
    [Serializable] public sealed class OfficialExploreUiRule { public int Order; }
    [Serializable] public sealed class OfficialExploreQualityText { public int ThemeColorType; public int[] TypeList; public OfficialExploreTextColor[] TextColors; }
    [Serializable] public sealed class OfficialExploreTextColor { public UnityEngine.Color Color; public int EnableGradient; public OfficialExploreGradient GradientColor; }
    [Serializable] public sealed class OfficialExploreGradient
    {
        public UnityEngine.Color key0, key1, key2, key3, key4, key5, key6, key7;
        public int ctime0, ctime1, ctime2, ctime3, ctime4, ctime5, ctime6, ctime7;
        public int atime0, atime1, atime2, atime3, atime4, atime5, atime6, atime7;
        public int m_NumColorKeys, m_NumAlphaKeys;
        public UnityEngine.Gradient Value()
        {
            var keys = new[] { key0, key1, key2, key3, key4, key5, key6, key7 };
            var times = new[] { ctime0, ctime1, ctime2, ctime3, ctime4, ctime5, ctime6, ctime7 };
            var alphaTimes = new[] { atime0, atime1, atime2, atime3, atime4, atime5, atime6, atime7 };
            var colors = new UnityEngine.GradientColorKey[m_NumColorKeys];
            var alphas = new UnityEngine.GradientAlphaKey[m_NumAlphaKeys];
            for (int i = 0; i < colors.Length; i++) colors[i] = new UnityEngine.GradientColorKey(keys[i], times[i] / 65535f);
            for (int i = 0; i < alphas.Length; i++) alphas[i] = new UnityEngine.GradientAlphaKey(keys[i].a, alphaTimes[i] / 65535f);
            var gradient = new UnityEngine.Gradient();
            gradient.SetKeys(colors, alphas);
            return gradient;
        }
    }
    [Serializable] public sealed class OfficialMap11RatingColors { public int type; public OfficialMap11RatingColor[] colors; }
    [Serializable] public sealed class OfficialMap11RatingColor { public int Key; public UnityEngine.Color Value; }
    [Serializable] public sealed class OfficialMap11MiniMapMark
    {
        public string name, sprite, description;
        public bool showOnEntry;
    }
    [Serializable] public sealed class OfficialMap11MiniMapPrefab
    {
        public string key;
        public OfficialMap11SceneNode[] nodes;
    }
    [Serializable] public sealed class OfficialMap11MiniMapBinding
    {
        public int placementId, completeState;
        public string key;
        public UnityEngine.Vector2 offset;
        public bool hideInEnd;
    }
    [Serializable] public sealed class OfficialMap11SceneNode
    {
        public string name, path, sprite;
        public int parentIndex, sortingOrder;
        public bool active, flipX, flipY;
        public UnityEngine.Vector3 position, scale;
        public UnityEngine.Quaternion rotation;
        public UnityEngine.Color color;
    }
    [Serializable] public sealed class OfficialMap11TopBarItem { public int type, itemId; public long maxCount; }
    [Serializable] public sealed class OfficialMap11PortraitSettings
    {
        public int TextureWidth, TextureHeight, mDepthBuffer;
        public float Size;
        public UnityEngine.Vector3 mOffset;
        public int[] mExcludeSkinTypes;
    }
    [Serializable] public sealed class OfficialMap11MiniMapSettings
    {
        public float MiniMapUICameraOrthographicSize, BoundExtendSize, MarkZoomThreshold;
        public int ShowMapSwitch;
        public OfficialMap11MiniMapCameraSetting CameraControllerSetting;
    }
    [Serializable] public sealed class OfficialMap11MiniMapCameraSetting { public float MinCameraOrthographicSize, MaxCameraOrthographicSize; }
    [Serializable] public sealed class OfficialMap11MiniMapTab
    {
        public int mapClassId, group;
        public string sprite;
        public OfficialMap11MiniMapSettings settings;
    }

    [Serializable] public sealed class OfficialMap11ItemIcon { public int itemId; public string sprite, qualitySprite, template; }
    [Serializable] public sealed class OfficialMap11GuidPrefab { public string guid, key; }
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
        public OfficialExploreGradient Gradient;
        public float Angle;
        public UnityEngine.Rect m_UVRect;
        public int m_RaycastTarget, m_Maskable;
        public int m_IgnoreReversedGraphics, m_BlockingObjects;
        public OfficialMap11LayerMask m_BlockingMask;
        public float m_Alpha;
        public bool m_Interactable, m_BlocksRaycasts, m_IgnoreParentGroups;
        public OfficialMap11UiMaterial material;
        public string maskTexture;
        public string sprite, m_FillRect, m_HandleRect, mSlider;
        public int m_Type, m_FillMethod, m_FillOrigin, m_FillClockwise, m_FillCenter, m_PreserveAspect;
        public float m_FillAmount, m_MinValue, m_MaxValue, m_Value, mSliderMinValue, mSliderMaxValue;
        public int m_Direction, m_WholeNumbers;
        public UnityEngine.Color m_Color;
        public OfficialMap11PortraitRequestor mRequestor;
        public string mIcon, mCountText, mBtn, mAddBtn, Content, myTaskItemSlot;
        public string Btn, BtnText, AmountText;
        public string defaultLoopPrefabName;
        public int totalCount, reverseDirection, reverseIndex, m_Horizontal, m_Vertical, m_MovementType, m_Inertia;
        public float m_Elasticity, m_DecelerationRate, m_ScrollSensitivity;
        public UnityEngine.Color mNormalColor, mTipColor, mLimitColor;
        public string myTaskTitle, TaskGoal, RewardSection, RewardSlot, mClaimBtn, mJumpBtn, mTraceBtn, mCanclTraceBtn;
        public string mCountdown, mGMBtn, mTaskLimitRemainTime, mRecommendCombatRatingItem;
        public string ProgressText, ContentConditonsContent, ResourceGo, ItemSlot, mText;
        public string[] ConditionItems;
        public OfficialMap11StateSwitcher mStateSwitcher, mMapTaskStateSwitcher, mIsRankOverStates;
        public OfficialMap11StateSwitcher IsOneTimesStateSwitcher;
        public OfficialMap11StateSwitcher StateSwitcher, mTipLevelState;
        public string mDisplay, mLayoutElement, mTipDotContent, mNodeDataText;
        public string mCostMoneyContent;
        public string[] Guiders;
        public string mContent;
        public string mEntry, mEntryContent, mEntryBtn, mFilter, mFilterContent, mButton;
        public string mDescription, mTabItem, mTextsControler, mActivityRecommondView;
        public string MapImage, Button, MapNameText;
        public OfficialMap11StateSwitcher SelectedSwitcher;
        public string[] MultiTextList, myActiveObj, myDeactiveObj, EntryBtns;
        public OfficialMap11StateSwitcher mSelectionTypeSwticher;
        public string mMyContent;
        public float mDefaultZoomThreshold;
        public OfficialMap11StateSwitcher mActiveSwitcher, mShowSwitcher;
        public string Slider, CameraController, PanelTitle, myInput, mMiniMapCamera;
        public float mySizeMin, mySizeMax, ScaleFactor;
        public float nearClip, farClip, orthographicSize;
        public UnityEngine.Color backgroundColor;
        public int clearFlags;
        public Zjcs.Official.WorldUI.DragControllerData dragSettings;
        public OfficialMap11StateSwitcher mChangeMapTabSwitcher;
        public string mCombatRatingText;
        public int mCombatRateColorType;
        public string[] mGraphics, mSprites, mTmpMediators;
        public OfficialMap11UiClip[] clips;
        public string defaultClip;
        public bool playAutomatically;
        public Zjcs.Official.WorldUI.MeshClipperCapsuleArgs mMeshClipperCapsuleArgs;
        public string mAnimation, mBattomBoardChangeAni, mBattomBoardLeftToRight, mBattomBoardRightToLeft;
        public string mTalkContentTxt, mCharacterNameBack, mBlackBgImg, mDarkMask, mClickMask;
        public UnityEngine.Color mBlackBgColorDark, mBlackBgColorDarkLess;
        public float mWordInterval;
        public string TitleText, ContentText, HintGo, HintText, ConfirmBtn, CancelBtn, ConfirmText, CancelText, LockMaskWithTime;
        public string mTxt, mTmp, mShowRemainTimeInfo, mRemainTimeText, CancelBtnLayoutElement;
        public float mLockTimeDefault, LockTime, CancelBtnCustomWidth;
        public int mSingleLineAlignment, mMultiLineAlignment;
        public OfficialMap11StateSwitcher mLockedStateSwitcher;
        public string CostMoneyContent, MoneyContent, CostItemIcon, CostText, ItemContentObj, Slot, mSlot;
        public string QualityImg, QualityShadowImg, myText, myTextGo, myContent, BottomGo, myBottomText, ItemIconAttachment, mFakeIcon, mProgress, BgTransform;
        public string EquipedGo, LevelLimit, SubRankLimit, EquipedIconObj, MiniGemsContent, myStarInfo, mHasHinit;
        public string[] extraClearObjects;
        public string FlareStarItemIcon, DiamondItemIcon, ExchangeFixedTxt, CountSliderGo;
        public int mirrorType;
        public bool reversed;
        public OfficialMap11IconProxy mIconProxy;
        public OfficialMap11QualitySprite[] qualitySprites;
        public UnityEngine.Color EnoughColor, LackColor;
        public int m_Enabled;
        public int m_RenderMode, m_SortingOrder, m_SortingLayerID, m_AdditionalShaderChannelsFlag;
        public float m_PlaneDistance;
        public bool m_OverrideSorting;
        public int DisableTopAdapt, DisableBottomAdapt;
        public string mContextSlot, MiniMapBtn, mRawImage, MyRegionInfoText, MyRegionProgress;
        public string Text;
        public string myKey, TitleTxt, myStageName, myStageDesc, myDesc, mStoryTitle, mStoryContent, mStoryReadEndBtn, mStoryIamge;
        public OfficialMap11IconSlot mIconSlot;
        public string mCloseBtn, mTitleText, mDescContentText, mContentRoot, mInteractionBtn, mPeriodicityInteractionTips;
        public string mRectTransform, mEntryText, mModifyHpPercentDescText, mSpawnEntityDesc, mAwardContent, Anchor;
        public string myTouchBtn, myBtnText, myFuncDescText, mDialogContent, mExtraPropItemText;
        public string myStageIcon, myBgIcon;
        public string mWorldDialogEntityContent, myBattleBtn, myAwardSlot, myLevelText, ReWardDescText, mLayerText;
        public string mRewardSlot, mTeamBtn, mBattleBtn, mRecommendCombatRatingText, mCurrentCombatRatingText, mRankSuppressionText, mTeamInviteText;
        public OfficialMap11StateSwitcher mTeamStateSwitcher, mRankSuppressionSwitcher;
        public UnityEngine.Vector3 mFixedGlobalScale;
        public string[] Masks;
        public int IsOverrideMask, OccluderCount;
        public string mCountLanguage;
        public OfficialMap11StageClickData[] mDatas;
        public string mItemNameText, mLightImage, mParticle;
        public string mOutputAdditionItemNameText, mOutputAdditionItemCountText, mExtraContentSlot;
        public float mIntervalTime;
        public int mItemType, mIsHideQualityImg;
        public OfficialAwardListConfigDictionary mAwardListPanelItemConfigDict;
        public bool mClampWidth, mClampHeight, mClampContentMove;
        public UnityEngine.Vector2 mClampWidthRange, mClampHeightRange;
        public string m_Content, m_Viewport;
        public string mItemIconSlot;
        public OfficialMap11PreviewGroup[] mWorldStageInteractionPreviewGroups;
        public OfficialMap11PreviewAssets mStagePreviewAssetGuidProxy;
        public OfficialMap11ManagedReferences references;
        public int mNumberSize;
        public int m_StartCorner, m_StartAxis, m_Constraint, m_ConstraintCount, m_ChildAlignment;
        public OfficialMap11Padding m_Padding;
        public UnityEngine.Vector2 m_CellSize, m_Spacing;
        public float spacing;
        public float PaddingTop, PaddingBottom;
        public string mVerticalLayoutGroup;
        public float mMaxHeight, mMinHeight;
        public int mLayoutPriority;
        public float[] m_SpacingList;
        public float[] m_VerticalSpacingList, m_HorizontalSpacingList;
        public OfficialMap11GridSettings gridSettings;
        public int m_ChildForceExpandWidth, m_ChildForceExpandHeight, m_ChildControlWidth, m_ChildControlHeight;
        public int m_ChildScaleWidth, m_ChildScaleHeight, m_ReverseArrangement;
    }
    [Serializable] public sealed class OfficialMap11GridSettings { public UGUIJszx.GridLayoutSetting[] settings; }
    [Serializable] public sealed class OfficialMap11StageClickData { public int ClickType; public string Button; }
    [Serializable] public sealed class OfficialAwardListConfigDictionary { public OfficialAwardListConfigEntry[] mSerializeBuffer; }
    [Serializable] public sealed class OfficialAwardListConfigEntry { public int Key; public OfficialAwardListConfig Value; }
    [Serializable] public sealed class OfficialAwardListConfig
    {
        public OfficialMap11AssetGuid AssetGuidProxy;
        public string Path;
        public float DelayCloseTime;
    }
    [Serializable] public sealed class OfficialMap11PortraitRequestor { public string mRawImage, mAnimationName; }
    [Serializable] public sealed class OfficialMap11IconProxy { public string mImage; }
    [Serializable] public sealed class OfficialMap11IconSlot { public string mPreviewSlot; }
    [Serializable] public sealed class OfficialMap11Padding { public int m_Left, m_Right, m_Top, m_Bottom; }

    [Serializable] public sealed class OfficialMap11UiMaterial
    {
        public string name, shader;
        public OfficialMap11MaterialFloat[] floats;
        public OfficialMap11MaterialColor[] colors;
    }
    [Serializable] public sealed class OfficialMap11MaterialFloat { public string name; public float value; }
    [Serializable] public sealed class OfficialMap11MaterialColor { public string name; public float[] value; }
    [Serializable] public sealed class OfficialMap11QualitySprite { public int quality; public string sprite; }
    [Serializable] public sealed class OfficialMap11UiClip
    {
        public string field, name;
        public float duration;
        public bool loop;
        public OfficialMap11UiTrack[] tracks;
        public OfficialMap11UnknownTrack[] unknown;
        public OfficialMap11UiAnimationEvent[] events;
    }
    [Serializable] public sealed class OfficialMap11UiAnimationEvent
    {
        public string functionName, data;
        public float time, floatParameter;
        public int intParameter, messageOptions;
    }
    [Serializable] public sealed class OfficialMap11UnknownTrack { public string reason, property; public uint path_hash; }
    [Serializable] public sealed class OfficialMap11PreviewGroup { public string Slot, Content; }
    [Serializable] public sealed class OfficialMap11PreviewAssets { public OfficialMap11PreviewAsset[] mSerializeBuffer; }
    [Serializable] public sealed class OfficialMap11PreviewAsset { public int Key; public OfficialMap11AssetGuid Value; }
    [Serializable] public sealed class OfficialMap11AssetGuid { public string mAssetGuid; }
    [Serializable] public sealed class OfficialMap11ManagedReferences { public OfficialMap11ManagedReference[] RefIds; }
    [Serializable] public sealed class OfficialMap11ManagedReference { public long rid; public OfficialMap11ManagedType type; public OfficialMap11ManagedData data; }
    [Serializable] public sealed class OfficialMap11StateSwitcher { public string mOwner; public OfficialMap11ReferenceId[] mStateConfigs; }
    [Serializable] public sealed class OfficialMap11LayerMask { public int m_Bits; }
    [Serializable] public sealed class OfficialMap11ReferenceId { public long rid; }
    [Serializable] public sealed class OfficialMap11ManagedType { public string @class; }
    [Serializable] public sealed class OfficialMap11ManagedData
    {
        public OfficialMap11ObjectStateData mStateGameObjectData;
        public OfficialMap11ColorStateData mStateColorData;
        public OfficialMap11TextStateData mStateMultiLanguageTextData;
        public OfficialMap11TextStateData[] mStateMultiLanguageTextDatas;
    }
    [Serializable] public sealed class OfficialMap11StateValue { public int mValue; }
    [Serializable] public sealed class OfficialMap11ObjectStateData { public OfficialMap11ObjectState[] GoConfig; }
    [Serializable] public sealed class OfficialMap11ObjectState { public OfficialMap11StateValue State; public string[] GameObjects; }
    [Serializable] public sealed class OfficialMap11ColorStateData { public OfficialMap11ColorState[] GraphicConfigs; }
    [Serializable] public sealed class OfficialMap11ColorState { public string[] Graphics; public OfficialMap11ColorValues Color; }
    [Serializable] public sealed class OfficialMap11ColorValues { public OfficialMap11ColorValue[] mSerializeBuffer; }
    [Serializable] public sealed class OfficialMap11ColorValue { public OfficialMap11StateValue Key; public UnityEngine.Color Value; }
    [Serializable] public sealed class OfficialMap11TextStateData { public string Text; public OfficialMap11TextValues Keys; }
    [Serializable] public sealed class OfficialMap11TextValues { public OfficialMap11TextValue[] mSerializeBuffer; }
    [Serializable] public sealed class OfficialMap11TextValue { public OfficialMap11StateValue Key; public string Value; }
    [Serializable] public sealed class OfficialMap11UiTrack
    {
        public string path, property, componentType;
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
        public string propTemplate, propParent, blessingText, treasureContent, treasureParent, iconSlot, iconKey;
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
