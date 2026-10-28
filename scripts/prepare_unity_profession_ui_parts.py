"""Port original application UI part/separator orchestration."""
import json
from pathlib import Path
import re

from prepare_unity_profession_photographer import ROOT,CODE,RUNTIME,record,save,clean
from prepare_unity_profession_player_appearance import binding


def common(text):
    text=clean(text)
    text=re.sub(r'^using (?:AppearanceSystem.Runtime|Common|Framework(?:\.[\w.]+)?|Spine(?:\.Unity)?);\n','',text,flags=re.M)
    return text.replace('namespace AppearanceSystem.Runtime;\n','').replace('AssetOperationHandle<GameObject>','IProfessionAppearancePartAsset')


def log_blocks(text):
    pattern=r'(?m)^([ \t]+)GLogger(?:Error|Warn)InterpolatedStringHandler message = new GLogger(?:Error|Warn)InterpolatedStringHandler[^\n]*\n\1if \(enabled\)\n\1\{\n(?P<body>.*?)^\1\}\n\1Log\.(?P<level>Error|Warn)\([^\n]*\n'
    def replacement(match):
        pieces=[]
        for kind,argument in re.findall(r'message.Append(Literal|Formatted)\((.*?)\);',match['body']):
            pieces.append(argument if kind=='Literal' else '('+argument.split(',',1)[0]+')')
        message=' + '.join(pieces) or '"Original appearance part operation failed"'
        return match[1]+('Debug.LogWarning' if match['level']=='Warn' else 'Debug.LogError')+'('+message+');\n'
    return re.sub(pattern,replacement,text,flags=re.S|re.M)


def build():
    code_manifest=CODE.parent.parent/'code-manifest.json';manifest=json.loads(code_manifest.read_bytes())
    if manifest['manifest']!='871_156598':raise ValueError('UI part source version changed')
    assemblies=[]
    for name in ('Assembly-CSharp.dll','Console.dll'):
        path=CODE.parent/'managed/decoded'/name
        if record(path)['sha256']!=manifest['assemblies'][name]['decoded_sha256']:raise ValueError('UI part application assembly changed: '+name)
        assemblies.append(record(path))
    ports=[]
    for name in ('PlayerPartMode','AppearanceRenderType','SeparatorSlotData','PartEntry','PartsEntry'):
        source=CODE/'AppearanceSystem.Runtime'/(name+'.cs');text=common(source.read_text(encoding='utf-8'))
        if name=='PartsEntry':
            text=text.replace('AppearanceUILite appearanceUILite = default;','IProfessionAppearanceUnloadable appearanceUILite;').replace('TryGetComponent<AppearanceUILite>(ref appearanceUILite)','TryGetComponent<IProfessionAppearanceUnloadable>(out appearanceUILite)')
        ports.append(save(name,source,text))
    for name in ('IAppearancePart','IAppearanceSkeletonPart','PartSlot'):
        source=CODE/(name+'.cs');text=common(source.read_text(encoding='utf-8')).replace('BaseSkeletonFollower SkeletonFollower','IProfessionAppearancePartFollower SkeletonFollower').replace('public Slot Slot;','public IProfessionSkinClipSlot Slot;')
        ports.append(save(name,source,text))
    source=CODE/'AppearancePartHandler.cs';text=common(source.read_text(encoding='utf-8'))
    text=binding(text,'\t[Inject]','\tpublic SafeAction OnSkinChanged', '''    private IProfessionAppearancePartBindings mPartBindings;
    protected IProfessionAppearancePartBindings PartBindings => mPartBindings ?? ProfessionAppearancePartBindings.Required;
    public void ConfigureParts(IProfessionAppearancePartBindings bindings)
    { mPartBindings = bindings ?? throw new ArgumentNullException(nameof(bindings)); }

''')
    ports.append(save('AppearancePartHandler',source,text))
    source=CODE/'AppearanceUIPartHandler.cs';text=common(source.read_text(encoding='utf-8'))
    text=text.replace('protected SkeletonGraphic mSkeletonGraphic;','protected Component mSkeletonGraphic;\n\n    private IProfessionAppearancePartGraphic Graphic => (IProfessionAppearancePartGraphic)mSkeletonGraphic;')
    text=re.sub(r'(\w+)\.Deconstruct\(ref (\w+), ref (\w+)\);',r'\2 = \1.Key; \3 = \1.Value;',text)
    text=text.replace('\t\tSpineSkinInfoParser spineSkinInfoParser = Parsers.Instance.SpineSkinInfoParser;\n','').replace('AppearanceConfig.Value','PartBindings.Config').replace('spineSkinInfoParser.HasEffect(','PartBindings.HasEffect(')
    text=text.replace('mSkeletonGraphic.PrepareInstructionsAndRenderers','Graphic.PrepareInstructionsAndRenderers').replace('mSkeletonGraphic.Skeleton','Graphic.Skeleton').replace('mSkeletonGraphic.separator','Graphic.separator').replace('mSkeletonGraphic.SeparatorParts','Graphic.SeparatorParts').replace('mSkeletonGraphic.enableSeparatorSlots','Graphic.enableSeparatorSlots')
    text=text.replace('Skeleton skeleton = Graphic.Skeleton;','IProfessionAppearancePartSkeleton skeleton = Graphic.Skeleton;').replace('Slot[] items = skeleton.DrawOrder.Items;','IProfessionSkinClipSlot[] items = skeleton.DrawOrder;').replace('SubmeshInstruction val =','ProfessionAppearancePartInstruction val =')
    text=re.sub(r'\bSlot (val\d*) =',r'IProfessionSkinClipSlot \1 =',text).replace('FindPartSlot(Slot slot)','FindPartSlot(IProfessionSkinClipSlot slot)').replace('GetOrCreatePartSlot(Slot slot)','GetOrCreatePartSlot(IProfessionSkinClipSlot slot)')
    text=text.replace('AppearanceAddressUtils.GetPlayerPartPrefabUI','ProfessionAppearancePartPaths.GetPlayerPartPrefabUI').replace('AppearanceAddressUtils.GetSkinEffectPath','ProfessionAppearancePartPaths.GetSkinEffectPath')
    text=text.replace('TryGetComponent<IAppearancePart>(ref appearancePart)','TryGetComponent<IAppearancePart>(out appearancePart)').replace('TryGetComponent<SeparatorSlotData>(ref separatorSlotData)','TryGetComponent<SeparatorSlotData>(out separatorSlotData)').replace('TryPop(ref partSlot)','TryPop(out partSlot)')
    text=text.replace('ResourceLoader.LoadPrefab(path)','PartBindings.LoadPrefab(path)').replace('ServiceProviderExtensions.Instantiate(ServiceProvider, result, mSlot, false)','PartBindings.Instantiate(result, mSlot, false)')
    text=log_blocks(text)
    for token in ('GLogger','[Inject]','Parsers.Instance','SpineSkinInfoParser','using Spine','SubmeshInstruction','DrawOrder.Items','TryPop(ref','ServiceProviderExtensions'):
        if token in text:raise ValueError('Unresolved UI part dependency: '+token)
    ports.append(save('AppearanceUIPartHandler',source,text))
    source=CODE/'AppearanceSystem.Runtime/AppearanceAddressUtils.cs';text=source.read_text(encoding='utf-8')
    a=text.index('\tpublic static string GetPlayerPartPrefabRenderer(');b=text.index('\n\tpublic static string GetPlayerPartToolPrefabPath(',a)
    methods=text[a:b]
    ports.append(save('ProfessionAppearancePartPaths',source,'public static class ProfessionAppearancePartPaths\n{\n    private static readonly string[] sKPartModeSuffix = new string[3] { "", "Integrated", "Home" };\n'+methods+'\n}\n'))
    bindings=RUNTIME/'ProfessionAppearancePartBindings.cs';validation=ROOT/'unity/Assets/Game/Editor/ProfessionUI/ProfessionAppearancePartsValidation.cs'
    proof=dict(format=1,manifest='871_156598',code_manifest=record(code_manifest),assemblies=assemblies,ports=ports,generator=record(Path(__file__)),
        bindings=[record(p) for p in (bindings,bindings.with_name(bindings.name+'.meta'))],validation=[record(p) for p in (validation,validation.with_name(validation.name+'.meta'))],
        dependencies=[record(ROOT/'assets/reference/profession-ui/player-weapon-source-manifest.json')],
        operation='Migrate every original application AppearancePartHandler/AppearanceUIPartHandler, parts entries, part-slot grouping, part interfaces, separator metadata and part-mode enums. Scope original address utility to its three part/effect path methods. Adapt consumed skeleton slots/draw order/separator instructions and follower/unload operations to explicit independent native boundaries. Preserve fields, filtering, cache/release/load/initialization ordering, dynamic separators, extra queues, inherited event timing, parent-transform defaults and original error/handle behavior. No skeleton SDK implementation is used.',
        validated_scope='Unity 2022.3.57f1c2 Play Mode passed across real frames: source filtering/caller ownership, queued and immediate extras, reverse effect extraction, initialization/event order, skin cache, separator identity/grouping/front-back placement, default world-transform preservation, dynamic guards/missing slots, original LateUpdate dirty reset, deferred destruction, texture-unload/handle-release order and original null-prefab handle behavior. Skeleton/follower/resource/separator-renderer endpoints are fixtures. Full actor rendering and profession UI remain unbound.',
        limitations=['Application orchestration only. The independent skeleton graphic must supply original separator boundaries and transforms; source part prefabs, real followers/textures and production resource bindings require integration. No UI geometry, layout or art is substituted.'])
    (ROOT/'assets/reference/profession-ui/ui-parts-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Ported original UI appearance part/separator orchestration.')


if __name__=='__main__':build()
