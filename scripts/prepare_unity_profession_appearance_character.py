"""Copy original character appearance lifecycle through explicit native asset/part bindings."""
import hashlib
import json
from pathlib import Path
import re
import shutil

from prepare_unity_profession_photographer import ROOT, CODE, RUNTIME, save, record, clean
from prepare_unity_profession_native_inputs import CACHE


def build():
    manifest=CODE.parent.parent/'code-manifest.json';metadata=json.loads(manifest.read_bytes())
    if metadata['manifest']!='871_156598':raise ValueError('Original character source version changed')
    assemblies=[]
    for name in ('Assembly-CSharp.dll','Console.dll'):
        path=CODE.parent/'managed/decoded'/name
        if hashlib.sha256(path.read_bytes()).hexdigest()!=metadata['assemblies'][name]['decoded_sha256']:raise ValueError('Original assembly changed: '+name)
        assemblies.append(record(path))
    ports=[]
    for name in ('HsvChange','AppearanceRuntimeData','IAppearanceComponent'):
        source=CODE/'AppearanceSystem.Runtime'/(name+'.cs');text=clean(source.read_text(encoding='utf-8'))
        text=text.replace('using Common;\n','').replace('using Spine.Unity;\n','').replace('namespace AppearanceSystem.Runtime;\n','')
        text=text.replace('SkeletonDataAsset skeletonDataAsset','ScriptableObject skeletonDataAsset').replace('SpineAnimationSplitBinarySO spineAnimationSplitBinarySo','ScriptableObject spineAnimationSplitBinarySo')
        ports.append(save(name,source,text))
    source=CODE.parent/'common-source/Common/Sex.cs';text=source.read_text(encoding='utf-8').replace('namespace Common;\n','');ports.append(save('Sex',source,text))
    source=CODE/'AppearanceSystem.Runtime/AppearanceAddressUtils.cs';text=clean(source.read_text(encoding='utf-8'))
    a=text.index('\tpublic static string GetPlayerSkeletonAssetPath');b=text.index('\tpublic static string GetTextureLoaderPath',a)
    methods='\n'.join(line.rstrip() for line in text[a:b].splitlines())+'\n'
    ports.append(save('ProfessionAppearanceCharacterPaths',source,'public static class ProfessionAppearanceCharacterPaths\n{\n'+methods+'}\n'))
    source=CODE/'AppearanceSystem.Runtime/AppearanceCharacter.cs';text=clean(source.read_text(encoding='utf-8'))
    for using in ('using Common;\n','using Framework;\n','using Framework.DependencyInjection;\n','using Framework.Logging;\n','using Framework.Logging.InterpolatedStringHandler;\n','using Spine.Unity;\n'):
        text=text.replace(using,'')
    text=text.replace('namespace AppearanceSystem.Runtime;\n','').replace('private AppearancePartHandler mPartHandler;','private Component mPartHandler;')
    a=text.index('\t[Inject]');b=text.index('\tpublic IAppearanceComponent AppearanceComponent',a)
    text=text[:a]+'''    private IProfessionAppearanceCharacterBindings mBindings;
    private IProfessionAppearanceCharacterBindings Bindings => mBindings ?? ProfessionAppearanceCharacterBindings.Required;
    public void Configure(IProfessionAppearanceCharacterBindings bindings) { mBindings = bindings ?? throw new ArgumentNullException(nameof(bindings)); }

'''+text[b:]
    text=text.replace('AppearanceAddressUtils.GetPlayer','ProfessionAppearanceCharacterPaths.GetPlayer')
    a=text.index('\t\tAssetOperationHandle<SkeletonDataAsset>');b=text.index('\t\tif (!Object.op_Implicit',a)
    text=text[:a]+'''        IProfessionAppearanceCharacterAsset assetOperationHandle = (IProfessionAppearanceCharacterAsset)(mDisposable = Bindings.LoadAsset(playerSkeletonAssetPath));
        IProfessionAppearanceCharacterAsset assetOperationHandle2 = (IProfessionAppearanceCharacterAsset)(mDisposableSplitAnimationSo = Bindings.LoadAsset(ProfessionAppearanceCharacterPaths.GetPlayerSplitAnimationSoPath(sex)));
        ScriptableObject result = assetOperationHandle.Result;
        ScriptableObject result2 = assetOperationHandle2.Result;
'''+text[b:]
    text=text.replace('!Object.op_Implicit((Object)(object)result)','!result').replace('AppearanceConfig.Value','Bindings.Config')
    a=text.index('\t\t\tGLoggerErrorInterpolatedStringHandler');b=text.index('\n\t\t}\n\t\telse',a)
    text=text[:a]+'            Debug.LogError("not exist character data");'+text[b:]
    a=text.index('\t\t\tGLoggerErrorInterpolatedStringHandler');b=text.index('\n\t\t\treturn false;',a)
    text=text[:a]+'            Debug.LogError("AppearanceComponent invalid");'+text[b:]
    text=text.replace('mPartHandler.SetSkins(skinMap);','Bindings.SetPartSkins(mPartHandler, skinMap);').replace('mPartHandler.Release(tryUnloadAllAssets: true);','Bindings.ReleaseParts(mPartHandler, tryUnloadAllAssets: true);')
    text=re.sub(r'(\w+)\.Deconstruct\(ref (\w+), ref (\w+)\);',r'\2 = \1.Key; \3 = \1.Value;',text)
    for forbidden in ('[Inject]','GLogger','AssetOperationHandle<','SkeletonDataAsset','SpineAnimationSplitBinarySO','using Spine.','.op_'):
        if forbidden in text:raise ValueError('Unresolved original character dependency: '+forbidden)
    ports.append(save('AppearanceCharacter',source,text))
    bindings=RUNTIME/'ProfessionAppearanceCharacterBindings.cs';validation=ROOT/'unity/Assets/Game/Editor/ProfessionUI/ProfessionAppearanceCharacterValidation.cs'
    closure=CACHE/'typed/appearance-character-binding-closure.json'
    shutil.copyfile(CACHE/'typed/native-bindings-main-dynamic.json',closure)
    proof=dict(format=1,manifest='871_156598',code_manifest=record(manifest),assemblies=assemblies,ports=ports,generator=record(Path(__file__)),native_binding_closure=record(closure),
        bindings=[record(bindings),record(bindings.with_name(bindings.name+'.meta'))],validation=[record(validation),record(validation.with_name(validation.name+'.meta'))],
        operation='Copy every original AppearanceCharacter method and serialized field, plus the full runtime-data/HSV/interface/sex types. Scope AppearanceAddressUtils to its three exact character source-path methods. Adapt asset handles, config and part calls through explicit native bindings and skeleton assets to ScriptableObject references. Retain release-before-validity, skin ordering, empty/part filtering, dye copying, append duplicates, handle overwrite/dispose order and event-free execution. No skeleton SDK implementation is used.',
        validated_scope='Unity 2022.3.57f1c2 Play Mode: source asset/result/dispose order (including the original overwritten split-handle behavior), exact sex enum values and character paths, release-before-validity, ordered skin filtering/append duplicates, part routing, copied dye entries, one-channel dye cache, null/invalid error branches, runtime-data reference/default constructors and destruction pass. Main native closure has 881 assets, 87 script types, 11 pending types and no missing GUIDs. Production native skeleton renderer and resource/part bindings are not installed by this component port.',
        limitations=['Actual source character assets, native skeleton renderer, part handlers and full profession UI remain unbound. This port does not complete skin composition, animation or persistence.'])
    (ROOT/'assets/reference/profession-ui/appearance-character-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Ported original character appearance lifecycle and data/path contracts.')


if __name__=='__main__':build()
