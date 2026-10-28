"""Migrate the original application weapon lifecycle without SDK implementation."""
import json
from pathlib import Path
import re

from prepare_unity_profession_photographer import ROOT,CODE,RUNTIME,record,save,clean
from prepare_unity_profession_player_appearance import strip,binding,logs


def build():
    code_manifest=CODE.parent.parent/'code-manifest.json';manifest=json.loads(code_manifest.read_bytes())
    if manifest['manifest']!='871_156598':raise ValueError('Weapon appearance code version changed')
    assemblies=[]
    for name in ('Assembly-CSharp.dll','Console.dll'):
        path=CODE.parent/'managed/decoded'/name
        if record(path)['sha256']!=manifest['assemblies'][name]['decoded_sha256']:raise ValueError('Official application assembly changed: '+name)
        assemblies.append(record(path))
    ports=[]
    source=CODE.parent/'common-source/Common/Profession.cs';ports.append(save('Profession',source,source.read_text(encoding='utf-8').replace('namespace Common;\n','')))
    source=CODE/'AppearanceSystem.Runtime/CharacterPartPoolEntry.cs';text=clean(source.read_text(encoding='utf-8')).replace('using Framework;\n','').replace('namespace AppearanceSystem.Runtime;\n','').replace('AssetOperationHandle<GameObject>','IProfessionAssetOperation<GameObject>')
    ports.append(save('CharacterPartPoolEntry',source,text))
    source=CODE/'AppearanceSystem.Runtime/ICharacterPartEntryProvider.cs';text=clean(source.read_text(encoding='utf-8')).replace('using Common;\n','').replace('namespace AppearanceSystem.Runtime;\n','');ports.append(save('ICharacterPartEntryProvider',source,text))
    source=CODE/'AppearanceSystem/PlayerWeapon.cs';text=strip(source.read_text(encoding='utf-8')).replace('using Spine.Unity;\n','').replace('using Unity.Mathematics;\n','')
    text=text.replace('public SkeletonAnimation WeaponSkeleton;','public Component WeaponSkeleton;').replace('public SkeletonGraphic WeaponSkeletonUI;','public Component WeaponSkeletonUI;').replace('ISkeletonComponent','IProfessionWeaponSkeleton')
    text=binding(text,'\t[Inject]','\tprotected IProfessionWeaponSkeleton SkeletonComponent', '''    private IProfessionPlayerWeaponBindings mWeaponBindings;
    private IProfessionPlayerWeaponBindings WeaponBindings => mWeaponBindings ?? ProfessionPlayerWeaponBindings.Required;
    public void ConfigureWeapon(IProfessionPlayerWeaponBindings bindings)
    { mWeaponBindings = bindings ?? throw new ArgumentNullException(nameof(bindings)); }

''')
    text=logs(text)
    text=text.replace('AppearanceAnimUtils.GetPlayerWeaponAnimName(','ProfessionWeaponAnimationNames.GetPlayerWeaponAnimName(WeaponBindings.Player, ')
    text=text.replace('SkeletonComponent.Skeleton.Skin.Name','SkeletonComponent.SkinName').replace('SkeletonComponent.Skeleton.SetSkin(value);','SkeletonComponent.SetSkin(value);')
    text=text.replace('AppearanceUtils.GetProfessionIdStr(','WeaponBindings.Player.GetProfessionIdStr(')
    text=text.replace('int.Zhanshi','(int)Profession.Zhanshi').replace('int.Fashi','(int)Profession.Fashi')
    text=text.replace('AppearanceAnimUtils.PlayAnimation(SkeletonComponent, playerWeaponAnimName, loop: true);','SkeletonComponent.PlayAnimation(playerWeaponAnimName, loop: true);')
    text=text.replace('mPartEntryProvider.','WeaponBindings.Parts.').replace('quaternion.op_Implicit(quaternion.identity)','Quaternion.identity')
    text=text.replace('\t\tGLoggerErrorInterpolatedStringHandler message;\n\t\tbool enabled;\n','')
    a=text.index('\t\t\tmessage = new GLoggerErrorInterpolatedStringHandler');b=text.index('\n\t\t\treturn;',a)
    text=text[:a]+'            Debug.LogError("PlayerWeapon UpdateSkin GetStandalonePart failed mLoadedEntry.GoPart == null, skinType:" + SkinType.pWeapon + " skinName:" + value);'+text[b:]
    a=text.index('\t\t\tmessage = new GLoggerErrorInterpolatedStringHandler');b=text.index('\n\t\t\tthrow;',a)
    text=text[:a]+'            Debug.LogException(exception);'+text[b:]
    for token in ('GLogger','Spine.Unity','AppearanceUtils.','AppearanceAnimUtils.','quaternion.','[Inject]','int.Zhanshi','int.Fashi'):
        if token in text:raise ValueError('Unresolved weapon dependency: '+token)
    ports.append(save('PlayerWeapon',source,text))
    source=CODE/'AppearanceSystem.Runtime/AppearanceAnimUtils.cs';text=source.read_text(encoding='utf-8')
    a=text.index('\tpublic static string GetPlayerWeaponAnimName(');b=text.index('\n\tpublic static Animation GetOrLoadSpineAnimation',a);method=text[a:b]
    method=method.replace('GetPlayerWeaponAnimName(Profession profession','GetPlayerWeaponAnimName(IProfessionPlayerAppearanceBindings bindings, int profession').replace('Profession baseProfession = AppearanceUtils.GetBaseProfession(profession);','int baseProfession = bindings.GetBaseProfession(profession);').replace('Profession.None','(int)Profession.None').replace('AppearanceUtils.GetProfessionId(baseProfession)','GetProfessionId(bindings, baseProfession)')
    utility=(CODE/'AppearanceSystem.Runtime/AppearanceUtils.cs').read_text(encoding='utf-8');a=utility.index('\tpublic static int GetProfessionId(');b=utility.index('\n\tpublic static string GetProfessionIdStr(',a)
    helper=utility[a:b].replace('GetProfessionId(Profession profession','GetProfessionId(IProfessionPlayerAppearanceBindings bindings, int profession').replace('GetProfessionIdStr(profession)','bindings.GetProfessionIdStr(profession)')
    ports.append(save('ProfessionWeaponAnimationNames',source,'using System.Collections.Generic;\npublic static class ProfessionWeaponAnimationNames\n{\n    private static readonly Dictionary<int, string> sPlayerWeaponAnimDict = new Dictionary<int, string>();\n'+method+'\n'+helper+'\n}\n'))
    bindings=RUNTIME/'ProfessionPlayerWeaponBindings.cs';validation=ROOT/'unity/Assets/Game/Editor/ProfessionUI/ProfessionPlayerWeaponValidation.cs'
    proof=dict(format=1,manifest='871_156598',code_manifest=record(code_manifest),assemblies=assemblies,ports=ports,generator=record(Path(__file__)),
        additional_source=[record(CODE/'AppearanceSystem.Runtime/AppearanceUtils.cs')],
        bindings=[record(p) for p in (bindings,bindings.with_name(bindings.name+'.meta'))],validation=[record(p) for p in (validation,validation.with_name(validation.name+'.meta'))],
        dependencies=[record(ROOT/'assets/reference/profession-ui/player-appearance-source-manifest.json')],
        operation='Migrate every original PlayerWeapon, CharacterPartPoolEntry and ICharacterPartEntryProvider method plus the complete profession enum. Scope application animation utilities to their exact weapon-name and profession-ID formatting methods. Adapt original serialized skeleton references to Components and their consumed operations to the independent native skeleton boundary; preserve field names, layout values, cached identity, direction fallback, prebound behavior, pool flags, callback rethrow/outer catch order and release state. Asset handles retain the existing native resource operation boundary. No skeleton SDK implementation is used.',
        validated_scope='Isolated Unity 2022.3.57f1c2 Play Mode passed: prebound world precedence and UI-only paths, standalone UI/world pool flags, inactive-child lookup, exact local transform reset, release/rent/callback/animation order, cached skin and repeated prebound SetSkin behavior, callback logging/rethrow/outer-catch then direction playback, invalid/destroyed rental state, missing component fallback, left/right/base-profession/skin names, source entry validity, captured/default scale and 20-second lifetime. GameObjects and native skeleton/resource/pool endpoints are fixtures. Production weapon prefabs, native skeletons, part pool and full profession page remain unbound.',
        limitations=['Application-layer migration only; it creates no substitute weapon art, character layout or skeleton renderer. Production pool/resources, source weapon model geometry/animation, full UI input/save and Player release require integration.'])
    (ROOT/'assets/reference/profession-ui/player-weapon-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Ported original weapon appearance, part-entry contract and animation-name methods.')


if __name__=='__main__':build()
