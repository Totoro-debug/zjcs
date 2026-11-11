using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.HomeMainPanelUI;

// Test-only animation owner: exercises the original lifecycle on the native
// currency subtree. This does not stand in for the complete HomeUI hierarchy.
public sealed partial class HomeMainPanelNativeRunner
{
    IEnumerator VerifyMainPanelLifecycle()
    {
        view.Root.SetActive(false);
        var group=view.Root.AddComponent<CanvasGroup>();
        var animator=view.Root.AddComponent<Animator>();
        animator.runtimeAnimatorController=Resources.Load<RuntimeAnimatorController>("Home/MainPanel/HomeUI");
        Require(animator.runtimeAnimatorController!=null,"original HomeUI controller explicit resource binding");
        animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        var panel=view.Root.AddComponent<HomeUI>();
        typeof(MainUIPanelWithAnimator).GetField("mAnimator",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(panel,animator);
        Require(panel.SceneUIGraphicType==ESceneUIGraphicType.Dialog,"original HomeUI graphic type");
        var lengths=AnimatorUtils.GetClipLengthDict(animator);
        Require(lengths.Count==2 && Math.Abs(lengths["MainCanvas_HomeUI_Show"]-23f/60)<.001f
            && Math.Abs(lengths["MainCanvas_HomeUI_Hide"]-7f/60)<.001f,"complete original clips and durations");
        Require(AnimatorUtils.GetClipLength(animator,"absent")==0,"original missing-clip branch");
        Require(AnimatorUtils.GetAnimationCurveLength(null)==0 && AnimatorUtils.GetAnimationCurveLength(new AnimationCurve())==0,
            "original empty-curve branches");
        var show=panel.Show(CancellationToken.None).AsTask();
        Require(view.Root.activeSelf && animator.GetBool("IsShow"),"Show activates before awaiting original animator state");
        float deadline=Time.realtimeSinceStartup+5;
        while(!show.IsCompleted){Require(Time.realtimeSinceStartup<deadline,"Show finishes using animator normalized time");yield return null;}
        show.GetAwaiter().GetResult();
        var state=animator.GetCurrentAnimatorStateInfo(0);
        Require(state.shortNameHash==Animator.StringToHash("Show") && state.normalizedTime>=1,"Show awaits the complete original state");
        Require(group.alpha==1 && group.interactable && group.blocksRaycasts,"original show root curves");
        Require(Subscriptions("OnItemCountUpdate")==3,"Show reborrows original currency observers");
        Capture("home-main-panel-visible.png");

        // Disable only the test Animator so cancellation cannot race the clip's
        // original m_IsActive curve. No production timing is changed.
        animator.enabled=false;
        using(var cancellation=new CancellationTokenSource())
        {
            var cancelled=panel.Hide(cancellation.Token).SuppressCancellationThrow().AsTask();
            Require(!cancelled.IsCompleted && !animator.GetBool("IsShow"),"Hide requests state before awaiting it");
            cancellation.Cancel();deadline=Time.realtimeSinceStartup+5;
            while(!cancelled.IsCompleted){Require(Time.realtimeSinceStartup<deadline,"Hide cancellation propagates");yield return null;}
            Require(cancelled.GetAwaiter().GetResult(),"Hide await reports cancellation through the original UniTask");
            Require(view.Root.activeSelf,"cancelled await does not execute the trailing deactivation; state="+animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
        }
        animator.enabled=true;
        var hide=panel.Hide(CancellationToken.None).AsTask();deadline=Time.realtimeSinceStartup+5;
        while(!hide.IsCompleted){Require(Time.realtimeSinceStartup<deadline,"Hide completes after the original state");yield return null;}
        hide.GetAwaiter().GetResult();
        Require(!view.Root.activeSelf,"Hide deactivates original panel");
        Require(Subscriptions("OnItemCountUpdate")==0 && Subscriptions("OnAutoIncreaseItemDataUpdate")==0,
            "panel deactivation returns the bar and cancels observers");
        var alreadyHidden=panel.Hide(CancellationToken.None);
        Require(alreadyHidden.Status==UniTaskStatus.Succeeded && !view.Root.activeSelf,"inactive Hide returns immediately");
        alreadyHidden.GetAwaiter().GetResult();

        show=panel.Show(CancellationToken.None).AsTask();deadline=Time.realtimeSinceStartup+5;
        while(!show.IsCompleted){Require(Time.realtimeSinceStartup<deadline,"Show restores a hidden panel");yield return null;}
        show.GetAwaiter().GetResult();
        Require(Subscriptions("OnItemCountUpdate")==3 && Subscriptions("OnAutoIncreaseItemDataUpdate")==1,
            "repeated lifecycle does not duplicate subscriptions");
        animator.speed=0;animator.Play("Hide",0,.5f);animator.Update(0);
        Require(Math.Abs(group.alpha-.5f)<.03f && !group.interactable && !group.blocksRaycasts && view.Root.activeSelf,
            "native Hide midpoint fades and blocks interaction before deactivation");
        Capture("home-main-panel-hide-midpoint.png");
        var otherState=panel.Hide(CancellationToken.None);
        Require(otherState.Status==UniTaskStatus.Succeeded && !view.Root.activeSelf,"non-Show state Hide returns immediately");
        otherState.GetAwaiter().GetResult();
    }
}
