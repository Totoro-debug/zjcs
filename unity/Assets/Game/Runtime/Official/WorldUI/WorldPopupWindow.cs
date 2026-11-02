using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Zjcs.Official.WorldUI
{
    // PopupWindow's original clip graph and show/hide order, with local coroutine completion.
    [RequireComponent(typeof(Animator), typeof(CanvasGroup))]
    public sealed class WorldPopupWindow : MonoBehaviour
    {
        public enum EState { None, Showing, Show, Hiding, Hide }
        public EState State { get; private set; }
        public AnimationClip mShowClip, mHideClip, mLoopClip;
        public event Action<WorldPopupWindow> OnShowing, OnHiding, OnShow, OnHide;
        public WorldGAction OnCanvasSortingOrderChanged = new WorldGAction();
        private Canvas mCanvas;
        public Canvas Canvas => mCanvas != null ? mCanvas : (mCanvas = GetComponent<Canvas>());
        private PlayableGraph graph;
        private AnimationPlayableOutput output;
        private Coroutine transition;
        private bool closing;
        private float mShowStartTime;
        private Action mMaskClickAction;
        private Playable lastPlayable;
        public bool IsPause { get; private set; }
        public event Action<GameObject> OnPauseEvent, OnResumeEvent;
        [SerializeField] private bool mCloseByClickMask;
        public bool CloseByClickMask { get => mCloseByClickMask; set => mCloseByClickMask = value; }

        private void Awake()
        {
            graph = PlayableGraph.Create(name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            output = AnimationPlayableOutput.Create(graph, name, GetComponent<Animator>());
        }

        private void OnEnable() => graph.Play();
        private void OnDisable() { if (graph.IsValid()) graph.Stop(); }
        private void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }

        public void Show()
        {
            closing = false;
            if (transition != null) StopCoroutine(transition);
            mShowStartTime = Time.time;
            State = EState.Showing;
            OnShowing?.Invoke(this);
            transition = StartCoroutine(Transition(mShowClip, () => { State = EState.Show; OnShow?.Invoke(this); Play(mLoopClip); }, true));
        }

        public void Hide(Action completed)
        {
            if (closing) return;
            closing = true;
            State = EState.Hiding;
            if (transition != null) StopCoroutine(transition);
            OnHiding?.Invoke(this);
            transition = StartCoroutine(Transition(mHideClip, () =>
            {
                State = EState.Hide;
                gameObject.SetActive(false);
                OnHide?.Invoke(this);
                completed?.Invoke();
            }, false));
        }

        public void SetMaskClickAction(Action action) => mMaskClickAction = action;

        public bool Pause()
        {
            if (IsPause) return false;
            IsPause = true;
            lastPlayable = output.GetSourcePlayable();
            output.SetSourcePlayable(Playable.Null);
            SetCanvasEnable(false);
            OnPauseEvent?.Invoke(gameObject);
            return true;
        }

        public bool Resume()
        {
            if (!IsPause) return false;
            IsPause = false;
            SetCanvasEnable(true);
            if (lastPlayable.IsValid()) output.SetSourcePlayable(lastPlayable);
            OnResumeEvent?.Invoke(gameObject);
            return true;
        }

        private void SetCanvasEnable(bool enable)
        {
            var group = GetComponent<CanvasGroup>();
            group.alpha = enable ? 1f : 0f;
            group.interactable = enable;
            group.blocksRaycasts = enable;
        }

        public void SetCanvasSortingOrder(int order)
        {
            Canvas.overrideSorting = true;
            Canvas.sortingOrder = order;
            OnCanvasSortingOrderChanged.Invoke();
        }

        public void OnMaskClick()
        {
            if (!CanCloseByMask()) return;
            if (mMaskClickAction != null) mMaskClickAction();
            else Hide(null);
        }

        private bool CanCloseByMask()
        {
            if (!CloseByClickMask) return false;
            if (Time.time - mShowStartTime <= .5f) return false;
            return true;
        }

        private IEnumerator Transition(AnimationClip clip, Action completed, bool supportPause)
        {
            Play(clip);
            if (clip != null)
            {
                float remaining = clip.length;
                while (remaining > 0f)
                {
                    yield return null;
                    if (!(IsPause & supportPause)) remaining -= Time.deltaTime;
                }
            }
            transition = null;
            completed?.Invoke();
        }

        private void Play(AnimationClip clip)
        {
            if (clip == null) return;
            Playable source = output.GetSourcePlayable();
            if (source.IsValid()) source.Destroy();
            AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
            output.SetSourcePlayable(playable);
            playable.SetTime(0);
            graph.Evaluate(0);
        }
    }
}
