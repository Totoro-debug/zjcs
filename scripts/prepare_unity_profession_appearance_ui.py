"""Copy original UI appearance material, mask, mesh and animation orchestration."""
import hashlib
import json
from pathlib import Path
import re

from prepare_unity_profession_photographer import ROOT, CODE, RUNTIME, save, record, clean


def build():
    manifest=CODE.parent.parent/'code-manifest.json';metadata=json.loads(manifest.read_bytes());assembly=CODE.parent/'managed/decoded/Assembly-CSharp.dll'
    if metadata['manifest']!='871_156598' or hashlib.sha256(assembly.read_bytes()).hexdigest()!=metadata['assemblies'][assembly.name]['decoded_sha256']:raise ValueError('Original UI appearance assembly changed')
    source=CODE/'AppearanceSystem.Runtime/IAppearanceAnimComponent.cs';text=source.read_text(encoding='utf-8').replace('using Spine;\n','').replace('namespace AppearanceSystem.Runtime;\n','').replace('AnimationState AnimState','IProfessionAppearanceAnimationState AnimState')
    ports=[save('IAppearanceAnimComponent',source,text)]
    source=CODE/'AppearanceSystem.Runtime/AppearanceUI.cs';text=clean(source.read_text(encoding='utf-8'))
    for using in ('using Framework.DependencyInjection;\n','using Framework.Logging;\n','using Framework.Logging.InterpolatedStringHandler;\n','using Spine;\n','using Spine.Unity;\n'):
        text=text.replace(using,'')
    text=text.replace('namespace AppearanceSystem.Runtime;\n','').replace('private SkeletonGraphic mSkeletonGraphic;','private Component mSkeletonGraphic;')
    a=text.index('\t[Inject]');b=text.index('\tpublic AppearanceUIPartHandler PartHandler',a)
    text=text[:a]+'''    private IProfessionAppearanceUIBindings mUIBindings;
    private IProfessionAppearanceUIBindings UIBindings => mUIBindings ?? ProfessionAppearanceUIBindings.Required;
    private IProfessionAppearanceUIAssetManager AppearanceAssetManager => UIBindings.AssetManager;
    private IProfessionAppearanceUIGraphic GraphicGraph => (IProfessionAppearanceUIGraphic)mSkeletonGraphic;
    public void ConfigureUI(IProfessionAppearanceUIBindings bindings) { mUIBindings = bindings ?? throw new ArgumentNullException(nameof(bindings)); }

'''+text[b:]
    text=text.replace('public AppearanceUIPartHandler PartHandler => (AppearanceUIPartHandler)mPartHandler;','public Component PartHandler => mPartHandler;')
    text=text.replace('public AnimationState AnimState','public IProfessionAppearanceAnimationState AnimState').replace('TrackEntry current2','IProfessionAppearanceTrack current2')
    text=text.replace('SkeletonDataAsset skeletonDataAsset','ScriptableObject skeletonDataAsset')
    text=text.replace('mSkeletonGraphic.','GraphicGraph.')
    text=text.replace('((Component)this).GetComponent<SkeletonGraphic>()','((Component)this).GetComponent<IProfessionAppearanceUIGraphic>() as Component')
    text=text.replace('HandleOnCustomDemandLoading(SkeletonGraphic skeletonGraphic)','HandleOnCustomDemandLoading(IProfessionAppearanceUIGraphic skeletonGraphic)')
    text=text.replace('skeletonGraphic.MaterialsMultipleCanvasRenderers.Items','skeletonGraphic.MaterialsMultipleCanvasRenderers').replace('skeletonGraphic.TexturesMultipleCanvasRenderers.Items','skeletonGraphic.TexturesMultipleCanvasRenderers')
    text=text.replace('AppearanceAnimUtils.TryLazyLoadSpineAnimation((ISkeletonComponent)(object)mSkeletonGraphic, GraphicGraph.startingAnimation, out var _)','UIBindings.TryLazyLoadSpineAnimation(GraphicGraph, GraphicGraph.startingAnimation, out var _)')
    text=text.replace('AppearanceAnimUtils.PlayAnimation(mSkeletonGraphic, animationName, loop)','UIBindings.PlayAnimation(GraphicGraph, animationName, loop)')
    text=text.replace('IAppearanceAssetManager appearanceAssetManager','IProfessionAppearanceUIAssetManager appearanceAssetManager')
    text=text.replace('((OnDemandTextureLoader)yooAssetTextureLoader).RequestLoadMaterialTextures','yooAssetTextureLoader.RequestLoadMaterialTextures')
    a=text.index('\t\t\tGLoggerErrorInterpolatedStringHandler');b=text.index('\n\t\t}\n\t}',a)
    text=text[:a]+'            Debug.LogError("AppearanceUI OnCheckMultipleMeshesValid 检查角色Mesh时发生异常，Exception:" + ex.Message + "\\n" + ex.StackTrace);'+text[b:]
    a=text.index('\t\tGLoggerErrorInterpolatedStringHandler');b=text.index('\n\t\tErrorStringBuilder.Clear();',a)
    text=text[:a]+'        Debug.LogError("AppearanceUI ClearIfMeshInvalid 网格已经清空，角色动画上下文信息：" + ErrorStringBuilder);'+text[b:]
    a=text.index('\t\t\tGLoggerInfoInterpolatedStringHandler');b=text.index('\n\t\t\treturn;',a)
    text=text[:a]+'            Debug.Log(name + " MaterialCallback but stencil still is 0");'+text[b:]
    text=text.replace('Object.op_Implicit((Object)(object)mTextureLoader)','mTextureLoader').replace('!Object.op_Implicit((Object)(object)mSkeletonGraphic)','!mSkeletonGraphic')
    text=re.sub(r'(\w+)\.Deconstruct\(ref (\w+), ref (\w+)\);',r'\2 = \1.Key; \3 = \1.Value;',text)
    for forbidden in ('[Inject]','GLogger','using Spine','.op_','OnDemandTextureLoader)','AppearanceAnimUtils.'):
        if forbidden in text:raise ValueError('Unresolved original UI appearance dependency: '+forbidden)
    if re.search(r'\b(?:SkeletonGraphic|SkeletonDataAsset|AnimationState|TrackEntry|AppearanceUIPartHandler)\s+\w',text):raise ValueError('Unresolved original UI appearance SDK type')
    ports.append(save('AppearanceUI',source,text))
    bindings=RUNTIME/'ProfessionAppearanceUIBindings.cs';validation=ROOT/'unity/Assets/Game/Editor/ProfessionUI/ProfessionAppearanceUIValidation.cs'
    proof=dict(format=1,manifest='871_156598',code_manifest=record(manifest),assembly=record(assembly),ports=ports,generator=record(Path(__file__)),
        dependencies=[record(ROOT/'assets/reference/profession-ui'/name) for name in ('appearance-spine-source-manifest.json','texture-loader-source-manifest.json')],
        bindings=[record(bindings),record(bindings.with_name(bindings.name+'.meta'))],validation=[record(validation),record(validation.with_name(validation.name+'.meta'))],
        operation='Copy every original AppearanceUI application method and serialized field plus its animation interface. Preserve multi-canvas initialization/events, mesh bounds guard and contextual error, mask-driven deferred skin/dye application, placeholder/material/texture request ordering, loaded references, original callbacks, animation and release. Adapt only native graphic/animation/resource service operations through explicit interfaces; use actual Unity Graphic/CanvasRenderer/mask/material APIs. No skeleton SDK implementation or replacement UI layout is used.',
        validated_scope='Isolated Unity Play Mode passed using actual Unity Graphic, CanvasRenderer, sorting canvases, nested masks and materials: initialization/ref/lazy-load ordering, mesh bounds errors and clearing, zero-stencil deferred skin/dye with original dictionary identity, dirty-callback retry/removal, texture placeholder/material reuse and loaded-reference ordering, animation dispatch, release and original destroy subscription behavior. Native skeleton endpoints are fixtures; source character rendering and full profession-page input/save are not validated.',
        limitations=['Native skeleton graphic, original character data/material loading, constraints and full profession-page interactions still require production integration.'])
    (ROOT/'assets/reference/profession-ui/appearance-ui-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Ported all original UI appearance orchestration methods.')


if __name__=='__main__':build()
