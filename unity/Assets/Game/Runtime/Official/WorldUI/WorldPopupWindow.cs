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
        public AnimationClip mShowClip, mHideClip, mLoopClip;
        public event Action<WorldPopupWindow> OnShowing, OnHiding, OnShow, OnHide;
        private PlayableGraph graph;
        private AnimationPlayableOutput output;
        private Coroutine transition;
        private bool closing;
        private float mShowStartTime;
        private Action mMaskClickAction;
        public bool CloseByClickMask { get; set; }

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
            OnShowing?.Invoke(this);
            transition = StartCoroutine(Transition(mShowClip, () => { OnShow?.Invoke(this); Play(mLoopClip); }));
        }

        public void Hide(Action completed)
        {
            if (closing) return;
            closing = true;
            if (transition != null) StopCoroutine(transition);
            OnHiding?.Invoke(this);
            transition = StartCoroutine(Transition(mHideClip, () =>
            {
                gameObject.SetActive(false);
                OnHide?.Invoke(this);
                completed?.Invoke();
            }));
        }

        public void SetMaskClickAction(Action action) => mMaskClickAction = action;

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

        private IEnumerator Transition(AnimationClip clip, Action completed)
        {
            Play(clip);
            if (clip != null) yield return new WaitForSeconds(clip.length);
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
