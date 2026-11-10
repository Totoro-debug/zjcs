using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.Home
{
    // Selected ScrollRectAutoScroll. UniTask frame waits become a scene-owned
    // coroutine; positions, linear interpolation and drag-lock branches remain.
    public sealed class DailyDungeonAwardAutoScroll : MonoBehaviour
    {
        [SerializeField] private ScrollRect mScrollRect;
        [SerializeField] private int mAxis = 1;
        [SerializeField] private float mFromPosition;
        [SerializeField] private float mToPosition = 1;
        [SerializeField] private bool mBlockDragOnPlaying = true;
        [SerializeField] private bool mLockDragWhenNotScrollable = true;
        private Coroutine running;
        private bool blocked, cachedDrag, forceLocked;
        public void LockDrag() { Stop(); forceLocked = true; RefreshDragLock(); }
        public void Play(float duration)
        {
            Stop(); forceLocked = false;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(mScrollRect.content);
            if (duration <= 0 || Distance() <= .01f)
            { SetPosition(mToPosition); RefreshDragLock(); return; }
            running = StartCoroutine(Scroll(duration));
            RefreshDragLock();
        }
        private IEnumerator Scroll(float duration)
        {
            mScrollRect.StopMovement(); SetPosition(mFromPosition);
            float elapsed = 0;
            while (elapsed < duration)
            {
                yield return null;
                elapsed += Time.deltaTime;
                SetPosition(Mathf.Lerp(mFromPosition, mToPosition, Mathf.Clamp01(elapsed / duration)));
            }
            running = null; RefreshDragLock();
        }
        public float CalcDurationByStep(float distance, float interval) => distance <= 0 || interval <= 0
            ? 0 : interval * Distance() / distance;
        private float Distance() => Mathf.Max(0, mAxis == 1
            ? mScrollRect.content.rect.height - mScrollRect.viewport.rect.height
            : mScrollRect.content.rect.width - mScrollRect.viewport.rect.width);
        private bool Drag { get => mAxis == 1 ? mScrollRect.vertical : mScrollRect.horizontal;
            set { if (mAxis == 1) mScrollRect.vertical = value; else mScrollRect.horizontal = value; } }
        private void SetPosition(float value)
        { if (mAxis == 1) mScrollRect.verticalNormalizedPosition = value; else mScrollRect.horizontalNormalizedPosition = value; }
        private void RefreshDragLock()
        {
            if (!mScrollRect) return;
            bool shouldBlock = forceLocked || (mBlockDragOnPlaying && running != null)
                || (mLockDragWhenNotScrollable && Distance() <= .01f);
            if (shouldBlock && !blocked) { blocked = true; cachedDrag = Drag; Drag = false; }
            else if (!shouldBlock && blocked) { blocked = false; Drag = cachedDrag; }
        }
        private void Stop()
        { if (running != null) StopCoroutine(running); running = null; RefreshDragLock(); }
        private void OnDisable() => Stop();
    }
}
