// Original airship scrolling sign, using the shared local language component.
using UnityEngine;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.HomeFurniture
{
    public class HomeScrollTextBord : MonoBehaviour
    {
        [SerializeField] private float mStartX;
        [SerializeField] private float mEndX;
        [SerializeField] private float mScrollSpeed;
        [SerializeField] private float mTextScrollEndSpace;
        [SerializeField] private WorldMultiLanguageText mText1;
        [SerializeField] private WorldMultiLanguageText mText2;
        private RectTransform mLeft;
        private RectTransform mRight;

        private void Awake()
        {
            mLeft = mText1.GetComponent<RectTransform>();
            mRight = mText2.GetComponent<RectTransform>();
            ResetPos();
        }

        private void Update()
        {
            Vector2 delta = Vector2.left * mScrollSpeed * Time.deltaTime;
            mLeft.anchoredPosition += delta;
            float end = mLeft.anchoredPosition.x + mLeft.rect.width + mTextScrollEndSpace;
            if (end < mStartX && mLeft.anchoredPosition.x < mEndX) mRight.anchoredPosition += delta;
            if (end <= mEndX)
            {
                RectTransform previousLeft = mLeft;
                mLeft = mRight;
                mRight = previousLeft;
                mRight.anchoredPosition = new Vector2(mStartX, mRight.anchoredPosition.y);
            }
        }

        public void SetContent(string key)
        {
            if (mText1.myKey == key) return;
            mText1.SetKey(key);
            mText2.SetKey(key);
            ResetPos();
        }

        private void ResetPos()
        {
            mLeft.anchoredPosition = new Vector2(mStartX, mLeft.anchoredPosition.y);
            mRight.anchoredPosition = new Vector2(mStartX, mRight.anchoredPosition.y);
        }
    }
}
