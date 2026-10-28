"""Port original application skin composition/lifecycle without any skeleton SDK implementation."""
import hashlib
import json
from pathlib import Path
import re

from prepare_unity_profession_photographer import ROOT, CODE, RUNTIME, save, record, clean


def build():
    manifest=CODE.parent.parent/'code-manifest.json';metadata=json.loads(manifest.read_bytes())
    assembly=CODE.parent/'managed/decoded/Assembly-CSharp.dll'
    if metadata['manifest']!='871_156598' or hashlib.sha256(assembly.read_bytes()).hexdigest()!=metadata['assemblies'][assembly.name]['decoded_sha256']:
        raise ValueError('Original appearance spine source changed')
    source=CODE/'AppearanceSystem.Runtime/AppearanceSpine.cs';text=clean(source.read_text(encoding='utf-8'))
    for using in ('using Framework;\n','using Framework.DependencyInjection;\n','using Framework.Logging;\n','using Framework.Logging.InterpolatedStringHandler;\n','using Spine;\n','using Spine.Unity;\n'):
        text=text.replace(using,'')
    text=text.replace('namespace AppearanceSystem.Runtime;\n','').replace('AppearancePartHandler mPartHandler','Component mPartHandler')
    text=text.replace('ISkeletonComponent','IProfessionAppearanceSkeletonComponent')
    text=re.sub(r'\bSkeletonDataAsset\b','ScriptableObject',text).replace('SpineAnimationSplitBinarySO','ScriptableObject')
    # Keep the original public asset property name despite its adapted type.
    text=text.replace('public ScriptableObject ScriptableObject =>','public ScriptableObject SkeletonDataAsset =>')
    a=text.index('\t[Inject]');b=text.index('\tpublic IProfessionAppearanceSkeletonComponent SkeletonComponent',a)
    text=text[:a]+'''    private IProfessionAppearanceSpineBindings mBindings;
    private IProfessionAppearanceSpineBindings Bindings => mBindings ?? ProfessionAppearanceSpineBindings.Required;
    public IProfessionAppearanceCharacterBindings ResourceLoader { get; private set; }
    public void Configure(IProfessionAppearanceSpineBindings bindings, IProfessionAppearanceCharacterBindings resourceLoader)
    { mBindings = bindings ?? throw new ArgumentNullException(nameof(bindings)); ResourceLoader = resourceLoader; }

'''+text[b:]
    text=text.replace('((Component)this).GetComponent<IProfessionAppearanceSkeletonComponent>()','Bindings.GetSkeletonComponent(this)')
    text=text.replace('private Skin mSkin;','private IProfessionAppearanceSkin mSkin;').replace('new Skin("combineSkin")','Bindings.CreateSkin("combineSkin")')
    text=text.replace('SkeletonData skeletonData = mSkeletonDataAsset.GetSkeletonData(true);','IProfessionAppearanceSkeletonData skeletonData = Bindings.GetSkeletonData(mSkeletonDataAsset, true);')
    text=text.replace('Skin val = skeletonData.FindSkin(skin);','IProfessionAppearanceSkin val = skeletonData.FindSkin(skin);')
    text=text.replace('Skin val = mSkin;','IProfessionAppearanceSkin val = mSkin;').replace('SetSkeletonSkins(Skin newSkin)','SetSkeletonSkins(IProfessionAppearanceSkin newSkin)')
    text=re.sub(r'\bSkeleton skeleton =', 'IProfessionAppearanceSkeleton skeleton =',text)
    text=text.replace('!Object.op_Implicit((Object)(object)skeletonDataAsset)','!skeletonDataAsset').replace('Object.op_Implicit((Object)(object)mSkeletonDataAsset)','mSkeletonDataAsset')
    a=text.index('\t\t\tGLoggerErrorInterpolatedStringHandler');b=text.index('\n\t\t}',a)
    text=text[:a]+'            Debug.LogError("OnSkinUpdate Error\\n" + exception);'+text[b:]
    a=text.index('\t\t\tGLoggerErrorInterpolatedStringHandler');b=text.index('\n\t\t}\n\t\telse if',a)
    text=text[:a]+'            Debug.LogError("skeleton asset is null");'+text[b:]
    a=text.index('\t\t\t\tGLoggerWarnInterpolatedStringHandler');b=text.index('\n\t\t\t}\n\t\t\telse',a)
    text=text[:a]+'                Debug.LogWarning("skin is not exist:" + skin);'+text[b:]
    a=text.index('\t\t\tGLoggerWarnInterpolatedStringHandler');b=text.index('\n\t\t\treturn;',a)
    text=text[:a]+'            Debug.LogWarning("不存在指定的皮肤材质:" + skinName);'+text[b:]
    a=text.index('\t\t\tGLoggerErrorInterpolatedStringHandler');b=text.index('\n\t\t\treturn null;',a)
    text=text[:a]+'            Debug.LogError("The skinName is not exist:" + skinName);'+text[b:]
    text=re.sub(r'(\w+)\.Deconstruct\(ref (\w+), ref (\w+)\);',r'\2 = \1.Key; \3 = \1.Value;',text)
    a=text.index('\t\t\tSlot[] items =');b=text.index('\n\t\t}\n\t\tskeleton.UpdateCache();',a)
    text=text[:a]+'''            for (int i = 0, count = skeleton.SlotCount; i < count; i++)
            {
                string attachmentName = skeleton.SetupAttachmentName(i);
                if (attachmentName != null)
                {
                    object attachment = newSkin.GetAttachment(i, attachmentName);
                    if (attachment != null) skeleton.SetSlotAttachment(i, attachment);
                }
            }'''+text[b:]
    for forbidden in ('[Inject]','GLogger','using Spine','.op_',' Skin ',' Skeleton ',' Slot','Attachment attachment','GetSkeletonData(true)'):
        if forbidden in text:raise ValueError('Unresolved original appearance spine dependency: '+forbidden)
    port=save('AppearanceSpine',source,text)
    bindings=RUNTIME/'ProfessionAppearanceSpineBindings.cs';validation=ROOT/'unity/Assets/Game/Editor/ProfessionUI/ProfessionAppearanceSpineValidation.cs'
    proof=dict(format=1,manifest='871_156598',code_manifest=record(manifest),assembly=record(assembly),ports=[port],generator=record(Path(__file__)),
        dependencies=[record(ROOT/'assets/reference/profession-ui'/name) for name in ('appearance-character-source-manifest.json','appearance-materials-source-manifest.json')],
        bindings=[record(bindings),record(bindings.with_name(bindings.name+'.meta'))],validation=[record(validation),record(validation.with_name(validation.name+'.meta'))],
        operation='Copy every original AppearanceSpine application method and serialized field. Preserve ordered skin composition, reuse/clear of the combined skin, missing-skin filtering, new versus same skin branches, setup-pose/event order, event exception handling, material/HSV calls and selective unload. Adapt skeleton data/skin/slot operations to explicit interfaces and native asset references to ScriptableObject. No skeleton SDK implementation is used.',
        validated_scope='Unity 2022.3.57f1c2 Play Mode: original ordered combined-skin reuse/clear/merge, missing-skin filtering, new versus same skin slot branches, setup/event order and caught multicast subscriber exception pass. Exact source HSV values reach the original translated material shader; config/material failure branches, null/same/different asset initialization, selective clear/cache/setup/unload, null skeleton/data/component branches and destruction pass. Skin/skeleton endpoints are fixtures; no SDK pose, production character render or full profession-page validation is claimed.',
        limitations=['Native renderer, asset/skin graph, animation constraints and full profession-page interactions remain required.'])
    (ROOT/'assets/reference/profession-ui/appearance-spine-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Ported all original application skin composition and skeleton lifecycle operations.')


if __name__=='__main__':build()
