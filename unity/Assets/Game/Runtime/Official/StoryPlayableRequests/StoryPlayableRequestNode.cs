using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.StoryPlayables;
namespace Zjcs.Official.StoryPlayableRequests
{
    public interface IStoryPrefabAssetProxy { GameObject Load(); void Unload(); }
    public interface IStoryHandleProxy
    {
        MyPlayable PlayPlayable(GameObject prefab,GameObject source,GameObject target,float speed=1f,GameObject parentGo=null,Dictionary<string,object> param=null,IMyPlayableNeedStop needStopObj=null);
        IStoryPrefabAssetProxy GetStoryRegisterAssetProxy(string key);
    }
    public interface IStoryPlayableEntityVisibility { void SetForceVisible(bool visible); }
    public interface IStoryWorldStagePlayableSupport { void Stop(); void Start(); }
    public sealed class StoryPlayableEntityBinding:MonoBehaviour
    {
        public IStoryPlayableEntityVisibility ClientVisibilitySupporter { get; set; }
    }
    // Consumed node-body boundary. The real graph owner must supply lifecycle,
    // dynamic input ports and original EC view lookups; absent bindings fail.
    public abstract class StoryPlayableRequestNode
    {
        public Action StartContext,EndContext,CleanContext;
        public Func<string,object> InputValue,InputValues;
        public Func<StoryPlayableEntityBinding,IStoryWorldStagePlayableSupport> GetWorldStageSupport;
        public virtual void OnStart()=>StartContext.Invoke();
        public virtual void OnEnd()=>EndContext.Invoke();
        public virtual void CleanWhileStoryAllEnd()=>CleanContext.Invoke();
        protected T GetInputValue<T>(string key)=>(T)InputValue(key);
        protected T[] GetInputValues<T>(string key)=>(T[])InputValues(key);
        // Exact original ClientRunNode continuation bodies (no graph transfer).
        protected virtual UniTask<StoryRunNodeResultCode> InternalRun(CancellationToken token)=>new UniTask<StoryRunNodeResultCode>(StoryRunNodeResultCode.OK);
        protected virtual UniTask<StoryRunNodeResultCode> InternalSkip(CancellationToken token)=>new UniTask<StoryRunNodeResultCode>(StoryRunNodeResultCode.OK);
        protected virtual UniTask<StoryRunNodeResultCode> AsyncTransRunToSkip(CancellationToken token)=>new UniTask<StoryRunNodeResultCode>(StoryRunNodeResultCode.OK);
        protected virtual StoryRunNodeResultCode GetCodeForTimeout()=>StoryRunNodeResultCode.SafeEnd;
        public StoryRunNodeResultCode TimeoutResult=>GetCodeForTimeout();
        public UniTask<StoryRunNodeResultCode> RunAsync(CancellationToken token)=>InternalRun(token);
        public UniTask<StoryRunNodeResultCode> SkipAsync(CancellationToken token)=>InternalSkip(token);
        public UniTask<StoryRunNodeResultCode> TransitionToSkipAsync(CancellationToken token)=>AsyncTransRunToSkip(token);
    }
}
