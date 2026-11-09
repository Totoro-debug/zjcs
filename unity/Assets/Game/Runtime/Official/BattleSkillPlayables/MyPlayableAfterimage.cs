using System.Collections.Generic;
using AppearanceSystem.SpineExt;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryPlayables;

[MyPlayableApply(new[] { typeof(SkeletonRenderer) })]
public class MyPlayableAfterimage : MyPlayable, IBattleAfterimagePlayable
{
    public List<AfterimageData> AfterimageDatas = new List<AfterimageData>();
    public RTRenderer RTRendererPrefab;
    public Transform RTRendererSlot;
    [Tooltip("单位：秒，-1表示无限持续")]
    public float Duration;

    private readonly MeshRendererZSortComparer comparer = new MeshRendererZSortComparer();
    private readonly Stack<RTRenderer> rendererPool = new Stack<RTRenderer>();
    private readonly List<MeshRenderer> renderers = new List<MeshRenderer>();
    private readonly List<Material> materials = new List<Material>();
    private SkeletonRenderer skeletonRenderer;
    private IAfterimageRTParamsProvider parameters;
    private IBattleAfterimageHost host;

    public void BindAfterimageHost(IBattleAfterimageHost value) => host = value;

    protected override void PlayImpl()
    {
        base.PlayImpl();
        skeletonRenderer = (SkeletonRenderer)myApplyComponent;
        parameters = GetComponent<IAfterimageRTParamsProvider>();
    }

    protected override bool ProcessFrameImpl(float deltaTime)
    {
        bool result = base.ProcessFrameImpl(deltaTime);
        if (skeletonRenderer == null || parameters == null || host == null) return result;
        float time = Time;
        foreach (AfterimageData data in AfterimageDatas)
        {
            if (time < data.RecordTime) continue;
            if (data.RTHandle == null) Capture(data);
            data.RTRenderer.SetColor(data.GetColor(time));
        }
        return result;
    }

    public override float GetDuration() => Duration;

    protected override void StopImpl()
    {
        if (skeletonRenderer != null && parameters != null && host != null)
        {
            for (int i = AfterimageDatas.Count - 1; i >= 0; i--)
            {
                AfterimageData data = AfterimageDatas[i];
                RenderTexture texture = data.RTHandle;
                RTRenderer renderer = data.RTRenderer;
                data.RTHandle = null;
                data.RTRenderer = null;
                host.ReturnRT(texture);
                ReturnRenderer(renderer);
            }
        }
        base.StopImpl();
    }

    private void Capture(AfterimageData data)
    {
        CollectRenderers();
        RenderTexture texture = host.GetRT(parameters.RTWidth, parameters.RTHeight);
        CommandBuffer command = CommandBufferPool.Get("Playable Afterimage CommandBuffer");
        try
        {
            Vector3 targetPosition = parameters.GetTargetPos(skeletonRenderer);
            command.SetRenderTarget(texture);
            command.ClearRenderTarget(true, true, Color.clear);
            command.SetViewProjectionMatrices(BattleAfterimageRTUtils.GetViewMatrix(targetPosition),
                BattleAfterimageRTUtils.GetProjectionMatrix(parameters.RTWidth, parameters.RTHeight, parameters.OrthoSize));
            foreach (MeshRenderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                renderer.GetSharedMaterials(materials);
                for (int i = 0; i < materials.Count; i++)
                    if (materials[i] != null) command.DrawRenderer(renderer, materials[i], i, 0);
            }
            Graphics.ExecuteCommandBuffer(command);
            RTRenderer display = GetRenderer();
            display.SetRT(texture);
            display.transform.position = targetPosition;
            data.RTRenderer = display;
            data.RTHandle = texture;
        }
        catch
        {
            host.ReturnRT(texture);
            throw;
        }
        finally
        {
            CommandBufferPool.Release(command);
            materials.Clear();
            renderers.Clear();
        }
    }

    private void CollectRenderers()
    {
        renderers.Clear();
        DynamicSkeletonRenderSeparator separator = skeletonRenderer.GetComponent<DynamicSkeletonRenderSeparator>();
        if (separator != null)
        {
            foreach (SkeletonPartsRenderer part in separator.partsRenderers)
                if (part != null && part.MeshRenderer != null) renderers.Add(part.MeshRenderer);
        }
        else if (skeletonRenderer.TryGetComponent(out MeshRenderer main)) renderers.Add(main);

        AppearanceRendererPartHandler appearance = skeletonRenderer.GetComponent<AppearanceRendererPartHandler>();
        if (appearance != null)
            foreach (AppearanceSkeletonPartEntry part in appearance.LoadedSkeletonDict.Values)
            {
                MeshRenderer renderer = part.Part.SkeletonTarget.GetComponent<MeshRenderer>();
                if (renderer != null) renderers.Add(renderer);
            }
        renderers.Sort(comparer);
    }

    private RTRenderer GetRenderer()
    {
        RTRenderer result = rendererPool.Count == 0
            ? Instantiate(RTRendererPrefab, RTRendererSlot, false)
            : rendererPool.Pop();
        result.gameObject.SetActive(true);
        return result;
    }

    private void ReturnRenderer(RTRenderer renderer)
    {
        if (renderer == null || renderer.gameObject == null) return;
        renderer.gameObject.SetActive(false);
        rendererPool.Push(renderer);
    }
}
