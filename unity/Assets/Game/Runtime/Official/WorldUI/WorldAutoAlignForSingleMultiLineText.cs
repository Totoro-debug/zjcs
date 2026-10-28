using System;
using System.Collections;
using TMPro;

namespace Zjcs.Official.WorldUI
{
    // AutoAlignForSingleMultiLineText: defer the original key refresh and line
    // measurement until end of frame, including reinitialization while enabled.
    public sealed class WorldAutoAlignForSingleMultiLineText : WorldInitializedUI
    {
        public WorldMultiLanguageText mTxt;
        public TextMeshProUGUI mTmp;
        public TextAlignmentOptions mSingleLineAlignment, mMultiLineAlignment;
        public event Action OnTextRefresh;
        private readonly UnityEngine.WaitForEndOfFrame endOfFrame = new UnityEngine.WaitForEndOfFrame();
        private string key;
        private object[] args;
        public bool IsSingleLine { get; private set; }

        public void SetText(string text) => SetKey("{0}", text);
        public void SetKey(string textKey, params object[] textArgs) => InitWrapper(() =>
        {
            key = textKey;
            args = textArgs;
        });

        protected override void OnEnableAndInited() => StartCoroutine(AutoAlign());
        protected override void OnDisableAndInited() => StopAllCoroutines();

        private IEnumerator AutoAlign()
        {
            yield return endOfFrame;
            mTxt.SetKey(key, args);
            mTmp.ForceMeshUpdate(false, false);
            IsSingleLine = mTmp.textInfo.lineCount <= 1;
            mTmp.alignment = IsSingleLine ? mSingleLineAlignment : mMultiLineAlignment;
            OnTextRefresh?.Invoke();
        }
    }
}
