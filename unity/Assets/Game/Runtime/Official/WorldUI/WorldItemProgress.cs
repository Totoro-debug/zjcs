using System;
using UnityEngine;
namespace Zjcs.Official.WorldUI
{
    // client-source/ItemProgress.cs. UIBase initialization and CultureNumberArg
    // are supplied locally; state switching and all four count branches are retained.
    public sealed class WorldItemProgress : MonoBehaviour
    {
        public enum ProgressType { Require, Submit, Custom, Cost }
        public Color mDefaultEnoughColor, mDefaultLackColor, mDefaultTextColor;
        public WorldMultiLanguageText mLeftCountText, mRightCountText;
        public bool mIsHasLeftTextUnit = true, mIsHasRightTextUnit = true;
        public WorldStateSwitcher<ProgressType> mStateSwitcher;
        [NonSerialized] public Func<long,string> Compact;
        private Color mEnoughColor, mLackColor, mTextColor;
        private long mLeftCount, mRightCount;
        private ProgressType mType;
        private string mTextKey;
        private object[] mTextArgs;
        private bool inited;
        private void Awake() => ResetColors();
        private void OnEnable() => Refresh();
        private void OnDisable() => ResetColors();
        private void ResetColors() { mEnoughColor=mDefaultEnoughColor; mLackColor=mDefaultLackColor; mTextColor=mDefaultTextColor; }
        public void Init(long leftCount,long rightCount,ProgressType type)
        { mLeftCount=leftCount;mRightCount=rightCount;mType=type;inited=true;Refresh(); }
        public void Init(string key,object[] args,Color? color)
        { mTextKey=key;mTextArgs=args;mTextColor=color??mDefaultTextColor;mType=ProgressType.Custom;inited=true;Refresh(); }
        public void SetColor(Color enoughColor,Color lackColor)
        { mEnoughColor=enoughColor;mLackColor=lackColor;Refresh(); }
        private object Number(long count,bool unit) => unit ? (object)(Compact ?? throw new InvalidOperationException("Original number formatter is unbound."))(count) : count;
        private void Refresh() { if (inited) UpdateCountText(); }
        private void UpdateCountText()
        {
            mStateSwitcher.SetState(mType);
            if(mType==ProgressType.Require) UpdateRequireTypeTextImpl();
            else if(mType==ProgressType.Submit) UpdateSubmitTypeTextImpl();
            else if(mType==ProgressType.Custom) UpdateCustomTypeTextImpl();
            else if(mType==ProgressType.Cost) UpdateCostTypeTextImpl();
        }
        private void UpdateRequireTypeTextImpl()
        {
            mLeftCountText.Color=mLeftCount>=mRightCount?mEnoughColor:mLackColor;
            mLeftCountText.SetKey("{0}",Number(mLeftCount,mIsHasLeftTextUnit));
            mRightCountText.Color=mTextColor;
            mRightCountText.SetKey("{0}",Number(mRightCount,mIsHasRightTextUnit));
        }
        private void UpdateSubmitTypeTextImpl()
        {
            mLeftCountText.Color=mRightCount>0?mEnoughColor:mLackColor;
            mLeftCountText.SetKey("{0}",Number(mRightCount,mIsHasLeftTextUnit));
            mRightCountText.Color=mTextColor;
            mRightCountText.SetKey("{0}",Number(mLeftCount,mIsHasRightTextUnit));
        }
        private void UpdateCostTypeTextImpl()
        {
            mLeftCountText.Color=mLeftCount>mRightCount?mLackColor:mEnoughColor;
            mLeftCountText.SetKey("{0}",Number(mLeftCount,mIsHasLeftTextUnit));
            mRightCountText.Color=mTextColor;
            mRightCountText.SetKey("{0}",Number(mRightCount,mIsHasRightTextUnit));
        }
        private void UpdateCustomTypeTextImpl()
        { mRightCountText.Color=mTextColor;mRightCountText.SetKey(mTextKey,mTextArgs); }
    }
}
