"""Port the original application skin/texture loading chain and its resource boundary."""
import hashlib
import json
from pathlib import Path
import shutil

from prepare_unity_profession_photographer import ROOT, CODE, RUNTIME, save, record
from prepare_unity_profession_native_inputs import CACHE


def build():
    manifest = CODE.parent.parent / 'code-manifest.json'
    metadata = json.loads(manifest.read_bytes())
    assemblies = []
    for name in ('Assembly-CSharp.dll', 'Framework.dll', 'Framework.FirstPass.Runtime.dll'):
        path = CODE.parent/'managed/decoded'/name
        if metadata['manifest'] != '871_156598' or hashlib.sha256(path.read_bytes()).hexdigest() != metadata['assemblies'][name]['decoded_sha256']:
            raise ValueError('Original texture loader assembly version changed')
        assemblies.append(record(path))
    ports = []
    entries = [('framework-firstpass-source', n) for n in ('IAssetProxy','AssetProxyBase','UnsafeAssetProxy')]
    entries += [('framework-source','SpineTextureAssetProxy')]
    entries += [('client-source/AppearanceSystem.Runtime', n) for n in ('SkinTextureConfig','SkinTextureAsset',
        'ITargetTextureReference','IOnDemandRequest','YooAssetTextureReference','YooAssetTextureHandle','YooAssetRequest',
        'LevelOfResolution','AppearanceOnDemandTextureLoader','YooAssetTextureLoader')]
    for folder, name in entries:
        source = CODE.parent/folder/(name+'.cs')
        text = source.read_text(encoding='utf-8')
        for using in ('using Spine.Unity;\n','using YooAsset;\n','using Framework.Logging;\n','using Framework.Logging.InterpolatedStringHandler;\n'):
            text = text.replace(using,'')
        text = text.replace('namespace AppearanceSystem.Runtime;\n','')
        if name == 'UnsafeAssetProxy':
            text = text.replace('AssetHandle','IProfessionUnsafeAssetHandle<T>')
            text = text.replace('((HandleBase)mHandle)', 'mHandle')
            text = text.replace('EOperationStatus status', 'int status').replace('mHandle.GetAssetObject<T>()','mHandle.Result').replace('val.GetAssetObject<T>()','val.Result')
            original = '\t\tAssetInfo assetInfoByGUID = YooAssets.GetAssetInfoByGUID(Guid, typeof(T));\n\t\tmHandle = YooAssets.LoadAssetSync(assetInfoByGUID);'
            if text.count(original) != 2: raise ValueError('Inspect original synchronous proxy loads')
            text = text.replace(original,'\t\tmHandle = ProfessionAppearanceTextureBindings.Required.LoadAssetSyncByGuid<T>(Guid);')
        elif name == 'SkinTextureAsset':
            text = text.replace('AssetGuidProxy<SkeletonDataAsset>','AssetGuidProxy<ScriptableObject>')
            a,b = text.index('\t\tGLoggerWarnInterpolatedStringHandler'),text.index('\t\treturn null;')
            text = text[:a]+'\t\tDebug.LogWarning("not exist the skin:" + skinName);\n'+text[b:]
        elif name == 'YooAssetTextureHandle':
            text = text.replace('EOperationStatus','int').replace('(int)0','0').replace('(int)1','1').replace('(int)2','2')
        elif name == 'AppearanceOnDemandTextureLoader':
            text = text.replace(': OnDemandTextureLoader, ITextureLoadCustomHandle', ': ScriptableObject')
            # Both inherited asset pointers are present in the verified native
            # serialization. Their objects and material access remain explicit.
            at = text.index('\n\t[Serializable]')
            text = text[:at]+'\n    public ScriptableObject atlasAsset;\n    public ScriptableObject skeletonDataAsset;\n'+text[at:]
            text = text.replace('((OnDemandTextureLoader)this).Clear(true);','Clear(true);').replace('base.atlasAsset','atlasAsset')
            text = text.replace('public override ', 'public virtual ')
            a = text.index('\t\tif (Object.op_Implicit((Object)(object)atlasAsset))')
            b = text.index('\n\t}\n', a)
            text = text[:a]+'''        if (atlasAsset)
        {
            Material val2 = ProfessionAppearanceTextureBindings.Required.GetAtlasMaterial(atlasAsset, materialIndex);
            if (val2)
            {
                val2.mainTexture = placeholderTextureMapping.placeholderTexture;
                RequestUnloadMaterial(val2);
            }
        }'''+text[b:]
        for forbidden in ('Spine.Unity','SpineAtlasAsset','SkeletonDataAsset','OnDemandTextureLoader)', 'ITextureLoadCustomHandle','YooAssets.', 'AssetInfo ', 'GLogger', 'HandleBase','EOperationStatus'):
            # SkeletonDataAssets is an original property name, not an SDK type.
            if forbidden == 'SkeletonDataAsset':
                if 'SkeletonDataAsset>' not in text: continue
            if forbidden in text: raise ValueError('Unresolved original dependency in '+name+': '+forbidden)
        ports.append(save(name,source,text))
    bindings = RUNTIME/'ProfessionAppearanceTextureBindings.cs'
    validation = ROOT/'unity/Assets/Game/Editor/ProfessionUI/ProfessionAppearanceTextureValidation.cs'
    closure = CACHE/'typed/texture-loader-binding-closure.json'
    shutil.copyfile(CACHE/'typed/native-bindings-main-dynamic.json', closure)
    proof = dict(format=1,manifest='871_156598',code_manifest=record(manifest),assemblies=assemblies,ports=ports,
        generator=record(Path(__file__)),native_binding_closure=record(closure),bindings=[record(bindings),record(bindings.with_name(bindings.name+'.meta'))],
        validation=[record(validation),record(validation.with_name(validation.name+'.meta'))],
        operation='Copy the complete original application proxy, request, skin configuration and on-demand texture loader methods. Keep original synchronous LoadAsync, handle/callback order, reference counters, frame-based expiry and original unimplemented query branches. Replace package operations and atlas-material access with an explicit local boundary; retain both original serialized asset pointers as ScriptableObjects. No skeleton SDK implementation is used.',
        validated_scope='Unity 2022.3.57f1c2 Play Mode: original synchronous proxy reuse/wait/release, synchronous LoadAsync callback queue and failed handle disposal, main/dye request state, null-result completion and release; placeholder mapping, first callback versus later ref replacement, atlas placeholder restoration, positive/negative reference counters, frame-age and forced expiry; original null-material dye exception and unimplemented queries; skin dictionary identity, duplicate failure and partial state pass. Atlas and resource handles are fixtures; no production native actor or profession-page validation.',
        limitations=['Production native atlas/skeleton data and local texture resource bindings remain unbound. Original configuration assets are not installed here. This code restoration alone does not complete appearance rendering or the profession UI.'])
    (ROOT/'assets/reference/profession-ui/texture-loader-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Ported',len(ports),'original application skin/texture loading types.')


if __name__ == '__main__': build()
