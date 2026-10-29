"""Port the original popup window, queue and complete marker widget dependencies."""
import argparse,json,re
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map13_sources import encode,write,meta,sha
from prepare_unity_map13_minimap_sources import record
from prepare_unity_map13_minimap_mark_core import method

SOURCE=ROOT/'.tools/source-packages/current/871_156598/official-code'
DEST=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapWindows'
NS='Zjcs.Official.World.Map13MiniMapWindows'

def build(r3):
    r3=r3.resolve()
    inputs=[];outputs=[]
    def read(p):inputs.append(record(p));return p.read_text(encoding='utf-8-sig').replace('\r\n','\n')
    def emit(name,text):
        p=DEST/name;write(p,text.encode());meta(p,'AssemblyDefinitionImporter' if p.suffix=='.asmdef' else 'MonoImporter' if p.suffix=='.cs' else 'TextScriptImporter');outputs.append(record(p))
    def wrap(text):
        text=re.sub(r'^using (Framework(?:\.[\w.]+)?|R3|Common|AK.Wwise);\n','',text,flags=re.M)
        at=re.search(r'(?m)^(?:\[|public )',text).start()
        return text[:at]+'namespace '+NS+'\n{\nusing Object=UnityEngine.Object;\nusing Zjcs.Official.StoryPlayables;\nusing Zjcs.Official.World.NativeLabelUI;\nusing OfficialWwiseEvent=Zjcs.Official.ProfessionUI.OfficialWwiseEvent;\n'+text[at:]+'\n}\n'
    fp=SOURCE/'framework-firstpass-source'
    original=read(fp/'PopupWindow.cs')
    # The compiler-generated IL state machines above these fields are recovered
    # below as async C#; each await, guard and finally remains in source order.
    text=original[:original.index('\t[CompilerGenerated]')]+original[original.index('\t[SerializeField]\n\tprivate AnimationClip mShowClip;'):]
    text=re.sub(r'^using (System.Diagnostics|System.Runtime.CompilerServices|System.Runtime.InteropServices|Cysharp.Threading.Tasks.CompilerServices|GFrameCore.Extensions.ZLogger|Microsoft.Extensions.Logging|ZLogger);\n','',text,flags=re.M)
    text=text.replace('\t[Inject]\n\tprivate IWindowManager WindowManager { get; set; }','\tpublic IMap13PopupWindowHost WindowManager { private get; set; }')
    text=text.replace('\n\t[Inject]\n\tprivate ILogger<PopupWindow> Logger { get; set; }','')
    text=text.replace('AnimationPlayableOutput.op_Implicit(AnimationPlayableOutput.Create(mGraph, "AnimationClip", Animator))','(PlayableOutput)AnimationPlayableOutput.Create(mGraph, "AnimationClip", Animator)')
    text=re.sub(r'\n\t\[AsyncStateMachine\([^\n]+\)\]\n','\n',text)
    bodies={
      'internal UniTask Show(bool instant = false)':'''public async UniTask Show(bool instant = false)
    {
        var cancellationDisposable = new CancellationDisposable();
        mSerialDisposable.Disposable = cancellationDisposable;
        await mTaskPipeline.StartTask(() => InternalShow(cancellationDisposable.Token));
        async UniTask InternalShow(CancellationToken cancellationToken)
        {
            if (IsVisible) { UnityEngine.Debug.Log(gameObject.name + " has activated!", gameObject); return; }
            if (State == EState.Showing || State == EState.Show) return;
            mShowStartTime = Time.time;
            State = EState.Showing;
            gameObject.SetActive(true);
            OnShowing.SafeInvoke(this);
            WindowManager.OnWindowsOpening.SafeInvoke(gameObject);
            try { await PlayPlayable(mShowClip, instant, true, cancellationToken); }
            catch (Exception) { throw; }
            finally
            {
                if (!mIsDestroying)
                {
                    State = EState.Show;
                    OnShow.SafeInvoke(this);
                    WindowManager.OnWindowOpened.SafeInvoke(gameObject);
                    PlayPlayable(mLoopClip, false);
                }
            }
        }
    }''',
      'internal UniTask Hide(bool instant = false)':'''public async UniTask Hide(bool instant = false)
    {
        var cancellationDisposable = new CancellationDisposable();
        mSerialDisposable.Disposable = cancellationDisposable;
        if (mDisableCancellation == null) mDisableCancellation = new CancellationDisposable();
        var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationDisposable.Token, mDisableCancellation.Token);
        await mTaskPipeline.StartTask(() => InternalHide(cancellationTokenSource.Token));
        async UniTask InternalHide(CancellationToken cancellationToken)
        {
            if (!IsVisible) { UnityEngine.Debug.Log(gameObject.name + " has unactivated!", gameObject); return; }
            if (State == EState.Hiding || State == EState.Hide) return;
            State = EState.Hiding;
            OnHiding.SafeInvoke(this);
            WindowManager.OnWindowClosing.SafeInvoke(gameObject);
            try { await PlayPlayable(mHideClip, instant, false, cancellationToken); }
            catch (Exception) { throw; }
            finally
            {
                if (!mIsDestroying)
                {
                    State = EState.Hide;
                    gameObject.SetActive(false);
                    OnHide.SafeInvoke(this);
                    WindowManager.OnWindowClosed.SafeInvoke(gameObject);
                }
            }
        }
    }''',
      'private UniTask PlayPlayable(AnimationClip clip, bool instant, bool supportPause, CancellationToken cancellationToken)':'''private async UniTask PlayPlayable(AnimationClip clip, bool instant, bool supportPause, CancellationToken cancellationToken)
    {
        if (clip == null) return;
        PlayPlayable(clip, instant);
        if (instant) return;
        float totalSeconds = clip.length;
        while (totalSeconds > 0f)
        {
            if (cancellationToken.IsCancellationRequested) return;
            await UniTask.NextFrame(cancellationToken, true);
            if (!(IsPause & supportPause)) totalSeconds -= Time.deltaTime;
        }
        await UniTask.Yield((PlayerLoopTiming)8, cancellationToken, true);
    }'''}
    for signature,body in bodies.items():text=text.replace(method(text,signature),body)
    # EState is before the removed generated types; all ordinary members remain.
    emit('PopupWindow.cs',wrap(text))
    for name in ['WindowDependence','WindowType','WindowMaskType','WindowLifeType']:emit(name+'.cs',wrap(read(fp/(name+'.cs'))))
    text=read(fp/'Framework/UniTaskPipeline.cs');text=text[text.index('\tprivate readonly Queue'):]
    text='using System;\nusing System.Collections.Generic;\nusing System.Threading;\nusing Cysharp.Threading.Tasks;\nusing UnityEngine;\npublic class UniTaskPipeline : IDisposable\n{\n'+text
    text=re.sub(r'\n\t\[AsyncStateMachine\([^\n]+\)\]\n','\n',text)
    for signature,generic in [('public UniTask StartTask(Func<UniTask> taskFactory)',False),('public UniTask<TResult> StartTask<TResult>(Func<UniTask<TResult>> taskFactory)',True)]:
        completion='UniTaskCompletionSource<TResult>' if generic else 'UniTaskCompletionSource'
        complete='completionSource.TrySetResult(await taskFactory());' if generic else 'await taskFactory(); completionSource.TrySetResult();'
        body=signature+'''\n    {
        ThrowIfDisposed();
        var completionSource = new '''+completion+'''();
        lock (mLockObject)
        {
            mTaskQueue.Enqueue(async () =>
            {
                try { '''+complete+''' }
                catch (Exception ex) { completionSource.TrySetException(ex); }
            });
            if (!mIsProcessing) { mIsProcessing = true; ProcessQueueAsync().Forget(); }
        }
        return completionSource.Task;
    }'''
        text=text.replace(method(text,signature),body)
    bodies={
      'public UniTask CancelAllAsync()':'''public async UniTask CancelAllAsync()
    {
        ThrowIfDisposed();
        mCancellationTokenSource?.Cancel();
        await mCurrentTask.SuppressCancellationThrow();
        lock (mLockObject) { mTaskQueue.Clear(); mIsProcessing = false; }
        mCancellationTokenSource?.Dispose();
        mCancellationTokenSource = new CancellationTokenSource();
    }''',
      'public UniTask WaitForCompletionAsync()':'''public async UniTask WaitForCompletionAsync()
    {
        while (true)
        {
            UniTask currentTask;
            lock (mLockObject)
            {
                if (mTaskQueue.Count == 0 && !mIsProcessing) return;
                currentTask = mCurrentTask;
            }
            await currentTask;
            await UniTask.Yield();
        }
    }''',
      'private UniTaskVoid ProcessQueueAsync()':'''private async UniTaskVoid ProcessQueueAsync()
    {
        while (!mIsDisposed)
        {
            Func<UniTask> taskFactory;
            lock (mLockObject)
            {
                if (mTaskQueue.Count == 0) { mIsProcessing = false; return; }
                taskFactory = mTaskQueue.Dequeue();
            }
            try { mCurrentTask = taskFactory(); await mCurrentTask; }
            catch (Exception ex) { Debug.LogError($"UniTaskPipeline error: {ex}"); }
        }
    }'''}
    for signature,body in bodies.items():text=text.replace(method(text,signature),body)
    emit('UniTaskPipeline.cs',wrap(text))
    for name in ['SerialDisposable','CancellationDisposable']:
        text=read(r3/(name+'.cs')).replace('namespace R3;','').replace('R3.Disposable.Empty','EmptyDisposable.Instance')
        if name=='SerialDisposable':text+='\ninternal sealed class EmptyDisposable:System.IDisposable { public static readonly EmptyDisposable Instance=new EmptyDisposable();public void Dispose() {} }\n'
        else:
            text=text.replace('public sealed class CancellationDisposable(CancellationTokenSource cancellationTokenSource) : IDisposable','public sealed class CancellationDisposable : IDisposable\n{\n    readonly CancellationTokenSource cancellationTokenSource;\n    public CancellationDisposable(CancellationTokenSource cancellationTokenSource) { this.cancellationTokenSource=cancellationTokenSource; }\n')
            # Replace the primary-constructor opening brace only.
            text=text.replace('\n\n{\n','\n',1)
        emit(name+'.cs',wrap('using System;\nusing System.Threading;\n'+text.strip()+'\n'))
    emit('R3-LICENSE.txt',read(r3/'LICENSE'))
    for name in ['CountRedDotBase','CountRedDotImage','MaintainGlobalScale','MaintainGlobalScaleUI','ConditionItem','PreviewTool']:
        text=read(SOURCE/('framework-source' if name=='PreviewTool' else 'client-source')/(name+'.cs'))
        text=text.replace('LanguageUtils.GetTipNumberWithPlus(count, 99L)','Map13WindowLanguage.GetTipNumberWithPlus(count, 99L)').replace('TryGetComponent<Canvas>(ref val3)','TryGetComponent<Canvas>(out val3)')
        if name=='MaintainGlobalScale':
            old=method(text,'private void OnValidate()')
            text=text.replace(old,'''private void OnValidate()
    {
        var children = GetComponentsInChildren<MaintainGlobalScale>();
        for (int i=0;i<children.Length;i++)
            if (children[i] != this) Debug.LogError("MaintainGlobalScale脚本在子节点中存在，需要删除当前节点的MaintainGlobalScale " + GetFullName(gameObject));
    }
    private static string GetFullName(GameObject value)
    {
        string name=value.name;
        for (Transform parent=value.transform.parent; parent!=null; parent=parent.parent) name=parent.name+"/"+name;
        return name;
    }''')
        emit(name+'.cs',wrap(text))
    text=read(fp/'PopupWindowSound.cs').replace('public Event ','public OfficialWwiseEvent ');emit('PopupWindowSound.cs',wrap(text))
    language=read(SOURCE/'common-source/Common/LanguageUtils.cs');number=method(language,'public static string GetTipNumberWithPlus(long num, long limit = 99L)')
    number=number.replace('LanguageManager.Instance.GetTextFormated(CommonNumberPlusKey, limit)','Zjcs.Official.World.Map13MiniMap.MiniMapMarkLanguage.Format("'+re.search(r'CommonNumberPlusKey\s*=>\s*"([^"]+)"',language)[1]+'", new object[] { limit })')
    emit('Map13WindowServices.cs','''using UnityEngine;
using Zjcs.Official.StoryPlayables;
namespace '''+NS+'''
{
    // Explicit host ports for the original window-manager events and close call.
    public interface IMap13PopupWindowHost
    {
        GAction<GameObject> OnWindowsOpening { get; }
        GAction<GameObject> OnWindowOpened { get; }
        GAction<GameObject> OnWindowClosing { get; }
        GAction<GameObject> OnWindowClosed { get; }
        void Close(GameObject window,bool instant);
    }
    public static class Map13WindowLanguage
    {
        '''+number+'''
    }
}
''')
    emit('Zjcs.Official.World.Map13MiniMapWindows.asmdef',encode(dict(name='Zjcs.Official.World.Map13MiniMapWindows',references=['Zjcs.Official.World.Map13MiniMapMarks','Zjcs.Official.World.NativeLabelUI','Zjcs.Official.StoryPlayables','Zjcs.Official.ProfessionUI','UniTask','Unity.ugui'],autoReferenced=True)).decode())
    meta(DEST,folder=True)
    write(ROOT/'assets/reference/official-map/map13-minimap-window-code-source-manifest.json',encode(dict(format=1,mapClassId=13,inputs=inputs,outputs=outputs,r3=dict(repository='https://github.com/Cysharp/R3',commit=(r3/'commit.txt').read_text().strip(),license='MIT'),generator=record(Path(__file__).resolve()),operation='Copy all original ordinary popup-window members, enums/dependence and widget classes. Recover compiler-generated async state machines into equivalent async C#, retaining all guards, cancellation, pipeline locking/queue, awaited operations, finally blocks, pause timer and event order. Replace logger backend only; inject explicit original window-manager event/close ports. Adapt upstream R3 primary constructor and namespace to Unity C#; preserve cancellation-on-dispose and serial atomic replacement.',limitations=['Formal native prefab binding, Play Mode animation/input and Windows Player remain required.'])))
    print('Original popup window and widget code:',len(outputs),'outputs')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--r3',type=Path,required=True);build(p.parse_args().r3)
