"""Copy game-owned attachment following; never read skeleton SDK implementation."""
import json
from pathlib import Path
import re
from prepare_unity_profession_photographer import ROOT,CODE,RUNTIME,record,save
from prepare_unity_profession_ui_parts import common
from prepare_unity_profession_player_appearance import binding


def build():
    manifest_path=CODE.parent.parent/'code-manifest.json';manifest=json.loads(manifest_path.read_bytes())
    if manifest['manifest']!='871_156598':raise ValueError('Follower source version changed')
    assemblies=[]
    for name in ('Assembly-CSharp.dll','Console.dll'):
        path=CODE.parent/'managed/decoded'/name
        if record(path)['sha256']!=manifest['assemblies'][name]['decoded_sha256']:raise ValueError('Follower source assembly changed')
        assemblies.append(record(path))
    ports=[]
    source=CODE/'ISkeletonFollower.cs';ports.append(save('ISkeletonFollower',source,common(source.read_text(encoding='utf8'))))
    source=CODE/'BaseSkeletonFollower.cs';text=common(source.read_text(encoding='utf8'))
    text=text.replace('MonoBehaviour, ISkeletonFollower','MonoBehaviour, ISkeletonFollower, IProfessionAppearancePartFollower')
    text=text.replace('\tpublic string BoneName;','\tpublic string BoneName;\n\n    string IProfessionAppearancePartFollower.SlotName => SlotName;\n    bool IProfessionAppearancePartFollower.ZFront => ZFront;')
    text=re.sub(r'\bBone (?=\w)', 'IProfessionFollowerBone ',text)
    text=re.sub(r'\bSkeleton (?=\w)', 'IProfessionFollowerSkeleton ',text)
    text=re.sub(r'\bSlot (?=\w)', 'IProfessionFollowerSlot ',text)
    text=text.replace('private Attachment mAttachment;','private object mAttachment;').replace('ISkeletonAnimationDelegate','Action<IProfessionFollowerAnimation>').replace('ISkeletonAnimation','IProfessionFollowerAnimation')
    text=text.replace('mSlot.Data.Index','mSlot.Index').replace('Slot[] items = mSkeleton.DrawOrder.Items;','IProfessionFollowerSlot[] items = mSkeleton.DrawOrder;')
    ports.append(save('BaseSkeletonFollower',source,text))
    for name,kind in (('SkeletonGraphicFollower','Graphic'),('SkeletonRendererFollower','Renderer')):
        source=CODE/(name+'.cs');text=common(source.read_text(encoding='utf8'))
        old='Skeleton'+kind;interface='IProfessionFollower'+kind
        text=text.replace('public '+old+' '+old+';','public Component '+old+';\n\n    private '+interface+' Target => ('+interface+')'+old+';')
        text=text.replace('target.GetComponent<'+old+'>()','(Component)target.GetComponent<'+interface+'>()')
        text=text.replace(old+' skeleton'+kind+' = '+old+';','Component skeleton'+kind+' = '+old+';')
        text=text.replace('ISkeletonAnimation','IProfessionFollowerAnimation')
        local='skeleton'+kind
        text=text.replace(local+'.Skeleton','(('+interface+')'+local+').Skeleton').replace(local+'.skeleton ==','(('+interface+')'+local+').Skeleton ==')
        text=text.replace(local+'.skeletonDataAsset.useCustomZSpacing','(('+interface+')'+local+').UseCustomZSpacing')
        text=text.replace(old+'.updateWhenInvisible','Target.UpdateWhenInvisible').replace(old+'.UpdateMode','Target.UpdateMode')
        text=text.replace(old+'.MeshGenerator.settings.zSpacing','Target.ZSpacing').replace(old+'.zSpacing','Target.ZSpacing')
        ports.append(save(name,source,text))
    source=CODE/'AppearanceEffectPart.cs';text=common(source.read_text(encoding='utf8')).replace('public BaseSkeletonFollower SkeletonFollower =>','public IProfessionAppearancePartFollower SkeletonFollower =>')
    ports.append(save('AppearanceEffectPart',source,text))
    source=CODE/'AppearanceSkeletonPart.cs';text=common(source.read_text(encoding='utf8'))
    text=text.replace('public BaseSkeletonFollower SkeletonFollower =>','public IProfessionAppearancePartFollower SkeletonFollower =>').replace('ISkeletonComponent','IProfessionPartAnimatedSkeleton')
    text=binding(text,'\t[Inject]','\tprotected IProfessionPartAnimatedSkeleton SkeletonComponent', '''    private IProfessionAppearancePartBindings mBindings;
    public void ConfigurePart(IProfessionAppearancePartBindings bindings)
    { mBindings = bindings ?? throw new System.ArgumentNullException(nameof(bindings)); }
    private IProfessionAppearancePartBindings PartBindings => mBindings ?? ProfessionAppearancePartBindings.Required;

''')
    text=text.replace('Object.op_Implicit((Object)(object)mSkeletonFollower)','mSkeletonFollower')
    text=text.replace('AppearanceAnimUtils.PlayAnimation(SkeletonComponent, sourceAnim, loop: true);','SkeletonComponent.PlayAnimation(sourceAnim, loop: true);')
    text=text.replace('Skeleton skeleton = SkeletonComponent.Skeleton;','string[] animations = SkeletonComponent.AnimationNames;').replace('Animation[] items = skeleton.Data.Animations.Items;','string[] items = animations;').replace('Animation[] items2 = skeleton.Data.Animations.Items;','string[] items2 = animations;').replace('foreach (Animation val in items2)','foreach (string val in items2)').replace('val.Name','val').replace('AppearanceConfig.Value','PartBindings.Config')
    ports.append(save('AppearanceSkeletonPart',source,text))
    for port in ports:
        text=(ROOT/port['runtime']['path']).read_text(encoding='utf8')
        for token in ('using Spine','ISkeletonAnimation','Spine.','[Inject]','SkeletonComponent.Skeleton','Data.Animations','DrawOrder.Items'):
            if token in text:raise ValueError('Unbound game follower dependency: '+token)
    bindings=RUNTIME/'ProfessionFollowerBindings.cs';validation=ROOT/'unity/Assets/Game/Editor/ProfessionUI/ProfessionFollowerValidation.cs'
    proof=dict(format=1,manifest='871_156598',code_manifest=record(manifest_path),assemblies=assemblies,ports=ports,generator=record(Path(__file__)),
        bindings=[record(p) for p in (bindings,bindings.with_name(bindings.name+'.meta'))],validation=[record(p) for p in (validation,validation.with_name(validation.name+'.meta'))],
        dependencies=[record(ROOT/'assets/reference/profession-ui/ui-parts-source-manifest.json')],
        operation='Copy complete game-owned base/UI/world follower and effect/skeleton part application classes. Preserve every field, branch, parent/local/world transform, attachment identity, visibility, draw-order Z/history, lifecycle/rebuild registration and animation-name lookup/caching. SDK type consumption is adapted to explicit native interfaces; no SDK implementation is read or ported.',
        validated_scope='Unity 2022.3.57f1c2 real Play Mode passed original local/world and RectTransform bone positions, UI x100/world spacing, custom and cached draw-order Z, rotation/reflection/scale flags, inactive-bone and attachment identity visibility, UI/world update modes, rebuild subscriptions and enable/disable/clear/destroy, effect/animated part target initialization and original animation mapping/cache/failure state. Native skeleton/graphic/resource endpoints are fixtures. Full actor rendering and profession UI remain unbound.')
    (ROOT/'assets/reference/profession-ui/followers-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf8',newline='\n')
    print('Ported game-owned followers and appearance parts.')


if __name__=='__main__':build()
