using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    // Original AutoAlignForSingleMultiLineTextContent, with the migrated text type.
    [RequireComponent(typeof(LayoutGroup))]
    public sealed class WorldAutoAlignForSingleMultiLineTextContent : MonoBehaviour
    {
        [SerializeField] private WorldAutoAlignForSingleMultiLineText mTxt;
        [SerializeField] private TextAnchor mSingleLineAlignment;
        [SerializeField] private TextAnchor mMultiLineAlignment;
        private LayoutGroup mLayoutGroup;
        private void Awake() => mLayoutGroup = GetComponent<LayoutGroup>();
        private void OnEnable()
        {
            mTxt.OnTextRefresh += OnTextRefresh;
            OnTextRefresh();
        }
        private void OnDisable()
        {
            if (mTxt != null) mTxt.OnTextRefresh -= OnTextRefresh;
        }
        private void OnTextRefresh()
            => mLayoutGroup.childAlignment = mTxt.IsSingleLine ? mSingleLineAlignment : mMultiLineAlignment;
    }
}
