"""Migrate original player appearance lifecycle and animation-name formatting."""
import hashlib
import json
from pathlib import Path
import re
import shutil

from prepare_unity_profession_photographer import ROOT, CODE, RUNTIME, clean, record, save
from prepare_unity_profession_native_inputs import CACHE


def strip(text):
    text=clean(text)
    text=re.sub(r'^using (?:AppearanceSystem.Runtime|Common|Framework(?:\.[\w.]+)?);\n','',text,flags=re.M)
    text=text.replace('namespace AppearanceSystem;\n','')
    text=text.replace('Profession.None','0')
    text=re.sub(r'(?<!\.)\bAppearanceData\b','object',text)
    text=re.sub(r'(?<!\.)\bProfession\b','int',text)
    return text


def binding(text,start,end,content):
    a=text.index(start);b=text.index(end,a)
    return text[:a]+content+text[b:]


def logs(text):
    pattern=r'\t+(?:GLoggerErrorInterpolatedStringHandler message = new GLoggerErrorInterpolatedStringHandler[^\n]*\n)(?:\t+if \(enabled\)\n\t+\{\n.*?\t+\}\n)?\t+Log.Error\([^\n]*\n'
    return re.sub(pattern,'            Debug.LogException(exception);\n',text,flags=re.S)


def build():
    code_manifest=CODE.parent.parent/'code-manifest.json';manifest=json.loads(code_manifest.read_bytes())
    if manifest['manifest']!='871_156598':raise ValueError('Player appearance code source changed')
    assemblies=[]
    for name in ('Assembly-CSharp.dll','Console.dll'):
        assembly=CODE.parent/'managed/decoded'/name
        if record(assembly)['sha256']!=manifest['assemblies'][name]['decoded_sha256']:raise ValueError('Official application assembly changed: '+name)
        assemblies.append(record(assembly))
    ports=[]
    for name in ('AppearanceOperateSource','EquipPos'):
        source=CODE.parent/'common-source/Common'/(name+'.cs')
        ports.append(save(name,source,source.read_text(encoding='utf-8').replace('namespace Common;\n','')))
    for name in ('AppearanceAnimNameCombineRule','AnimCombineRule','RuleFillType'):
        source=CODE.parent/'common-source'/(name+'.cs');text=source.read_text(encoding='utf-8').replace('using Framework;\n','').replace('[ScriptableObjectConfig]\n','')
        ports.append(save(name,source,text))
    source=CODE/'LiteInfoCallbackBase.cs';text=clean(source.read_text(encoding='utf-8'))
    text=text.replace('using Framework.Logging;\n','').replace('using Framework.Logging.InterpolatedStringHandler;\n','using UnityEngine;\n')
    text=text.replace('LiteInfoCallbackBase<T>','ProfessionLiteInfoCallbackBase<T>').replace('public LiteInfoCallbackBase(','public ProfessionLiteInfoCallbackBase(')
    a=text.index('\t\tcatch (Exception ex)');b=text.index('\n\tpublic void Release()',a)
    text=text[:a]+'''        catch (Exception ex)
        {
            if (!ex.ToString().StartsWith(kGameOBjectDestroyedMsg))
            {
                Debug.LogException(ex);
                return;
            }
            Debug.LogError(mCallback.Method.Name + "未释放");
            Release();
        }
    }
'''+text[b:]
    ports.append(save('ProfessionLiteInfoCallbackBase',source,text))
    source=CODE/'LiteInfoCallback.cs';text=source.read_text(encoding='utf-8').replace('using Common;\n','').replace('LiteInfoCallbackBase<PlayerLiteInfo>','ProfessionLiteInfoCallbackBase<ProfessionPlayerLiteAppearance>').replace('LiteInfoCallback','ProfessionLiteInfoCallback').replace('ProfessionProfessionLiteInfoCallbackBase','ProfessionLiteInfoCallbackBase').replace('Action<PlayerLiteInfo>','Action<ProfessionPlayerLiteAppearance>')
    ports.append(save('ProfessionLiteInfoCallback',source,text))
    source=CODE/'PlayerAppearanceInfo.cs';text=strip(source.read_text(encoding='utf-8')).replace('LiteInfoCallback','ProfessionLiteInfoCallback').replace('PlayerLiteInfo info','ProfessionPlayerLiteAppearance info')
    text=binding(text,'\t[Inject]','\tpublic bool Inited', '''    private IProfessionPlayerAppearanceBindings mPlayerBindings;
    protected IProfessionPlayerAppearanceBindings PlayerBindings => mPlayerBindings ?? ProfessionPlayerAppearanceBindings.Required;
    public void ConfigurePlayerAppearance(IProfessionPlayerAppearanceBindings bindings)
    { mPlayerBindings = bindings ?? throw new ArgumentNullException(nameof(bindings)); }

''')
    text=text.replace('PlayerLiteInfoManager.GetLiteInfo','PlayerBindings.GetLiteInfo').replace('MeManager.MainPlayer','PlayerBindings.MainPlayer').replace('Player mainPlayer','IProfessionPlayerAppearanceSource mainPlayer')
    text=text.replace('!mainPlayer.AppearanceData.Valid','!PlayerBindings.IsAppearanceValid(mainPlayer.AppearanceData)')
    a=text.index('\t\t\tforeach (KeyValuePair<SkinType, PlayerSkinDataWrap>');b=text.index('\n\t\t\tGLoggerErrorInterpolatedStringHandler',a)
    text=text[:a]+'''            foreach (string skinName in PlayerBindings.GetSkinNames(mainPlayer.AppearanceData))
                text = text + skinName + "\\n";'''+text[b:]
    text=logs(text)
    ports.append(save('PlayerAppearanceInfo',source,text))
    for name in ('APlayerAppearanceInfo','IAppearanceRuntimeDataOverride'):
        source=CODE/'AppearanceSystem'/(name+'.cs');ports.append(save(name,source,strip(source.read_text(encoding='utf-8'))))
    source=CODE/'AppearanceSystem/PlayerAppearance.cs';text=strip(source.read_text(encoding='utf-8'))
    text=binding(text,'\t[Inject]','\tpublic AppearanceRuntimeData AppearanceRuntimeData','')
    text=text.replace('AppearanceConfig.Value','PlayerBindings.Config').replace('AppearanceUtils.BuildAppearanceRuntimeData','PlayerBindings.BuildAppearanceRuntimeData')
    text=logs(text);ports.append(save('PlayerAppearance',source,text))
    source=CODE/'AppearanceSystem/PlayerAnimationAppearance.cs';text=strip(source.read_text(encoding='utf-8'))
    text=binding(text,'\t[Inject]','\tpublic bool AutoResetAnimation','')
    text=text.replace('int.None','0').replace('AppearanceUtils.GetBaseProfession','PlayerBindings.GetBaseProfession').replace('AppearanceAnimUtils.GetAnimName(AppearanceAnimNameCombineRule.Value,','ProfessionAppearanceAnimationNames.GetAnimName(PlayerBindings, PlayerBindings.AnimationRules,')
    text=text.replace('IAppearanceAnimComponent appearanceAnimComponent = default;','IAppearanceAnimComponent appearanceAnimComponent;').replace('TryGetComponent<IAppearanceAnimComponent>(ref appearanceAnimComponent)','TryGetComponent<IAppearanceAnimComponent>(out appearanceAnimComponent)')
    text=logs(text);ports.append(save('PlayerAnimationAppearance',source,text))
    source=CODE/'AppearanceSystem.Runtime/AppearanceAnimUtils.cs';text=source.read_text(encoding='utf-8')
    a=text.index('\tpublic static string GetAnimName(');b=text.index('\n\tpublic static string GetAnimName(',a+1);method=text[a:b]
    method=method.replace('GetAnimName(AppearanceAnimNameCombineRule','GetAnimName(IProfessionPlayerAppearanceBindings bindings, AppearanceAnimNameCombineRule')
    method=method.replace('Profession profession = Profession.None','int profession = 0').replace('AppearanceUtils.GetProfessionIdStr(profession)','bindings.GetProfessionIdStr(profession)').replace('AppearanceUtils.GetSexStr(sex)','sex == Sex.Male ? "M" : sex == Sex.Female ? "F" : null').replace('AppearanceUtils.GetFormatId(int.Parse(inputList[num++]))','GetFormatId(int.Parse(inputList[num++]))')
    text='''using System.Collections.Generic;
public static class ProfessionAppearanceAnimationNames
{
    private static readonly Dictionary<int, object[]> sFormatArrayDict = new Dictionary<int, object[]>();
    private static readonly Dictionary<int, string> sIdDict = new Dictionary<int, string>();
'''+method+'\n'
    utility=(CODE/'AppearanceSystem.Runtime/AppearanceUtils.cs').read_text(encoding='utf-8')
    a=utility.index('\tpublic static string GetFormatId(');b=utility.index('\n\tpublic static string GetNpcSkinName',a)
    text+=utility[a:b]+'\n}\n';ports.append(save('ProfessionAppearanceAnimationNames',source,text))
    source=CODE/'ProfessionUtils.cs';text=source.read_text(encoding='utf-8')
    a=text.index('\tpublic static void SetProfessionAppearance(');b=text.index('\n\tpublic static void CheckTaskStatus(',a);text=text[a:b]
    text=text.replace('SetProfessionAppearance(AppearanceConfig','SetProfessionAppearance(IProfessionAppearancePreviewBindings bindings, AppearanceConfig')
    text=text.replace('Profession profession','int profession').replace('AppearanceData appearanceData,','object appearanceData,')
    text=text.replace('AppearanceData appearanceData2 = appearanceData.Clone();','ProfessionAppearancePreviewData appearanceData2 = bindings.CloneAppearance(appearanceData);')
    text=text.replace('ProfessionBaseInfo professionBaseInfo = Parsers.Instance.ProfessionBaseInfoParser.Get(profession);','ProfessionAppearancePreviewDefinition professionBaseInfo = bindings.GetProfession(profession);').replace('Profession.None','0')
    text=text.replace('AppearanceRuntimeData runtimeData = AppearanceUtils.BuildAppearanceRuntimeData(sex, profession, appearanceData2, appearanceConfig.GetAppendSkins(appendSkinType));','AppearanceRuntimeData runtimeData = new AppearanceRuntimeData();\n        bindings.Player.BuildAppearanceRuntimeData(sex, profession, appearanceData2.Source, appearanceConfig.GetAppendSkins(appendSkinType), runtimeData);')
    ports.append(save('ProfessionAppearancePreview',source,'using System.Collections.Generic;\npublic static class ProfessionAppearancePreview\n{\n'+text+'\n}\n'))
    source=CODE/'ProfessionAppearance.cs';text=strip(source.read_text(encoding='utf-8'))
    text=binding(text,'\t[Inject]','\tpublic void InitAppearance', '''    private IProfessionAppearancePreviewBindings mPreviewBindings;
    private IProfessionAppearancePreviewBindings PreviewBindings => mPreviewBindings ?? ProfessionAppearancePreviewBindings.Required;
    public void ConfigurePreview(IProfessionAppearancePreviewBindings bindings)
    { mPreviewBindings = bindings ?? throw new System.ArgumentNullException(nameof(bindings)); }

''')
    text=text.replace('ProfessionUtils.SetProfessionAppearance(AppearanceConfig.Value,','ProfessionAppearancePreview.SetProfessionAppearance(PreviewBindings, PreviewBindings.Player.Config,')
    text=text.replace('AppearanceUI appearanceUI = default;','AppearanceUI appearanceUI;').replace('TryGetComponent<AppearanceUI>(ref appearanceUI)','TryGetComponent<AppearanceUI>(out appearanceUI)')
    ports.append(save('ProfessionAppearance',source,text))
    for p in ports:
        text=(ROOT/p['runtime']['path']).read_text(encoding='utf-8')
        for forbidden in ('GLogger','[Inject]','MeManager','using Common;','using Spine','ref appearanceAnimComponent','int.None'):
            if forbidden in text:raise ValueError('Unresolved player appearance dependency: '+forbidden)
    from official_apk import Apk
    from map13_native_schemas import NativeSchemas
    from prepare_unity_profession_reward_sprites import Dumper,fields,source_header
    import yaml
    apk=Apk.current();bundle='defaultpackage_assets_scriptableconfig_modules_appearance.bundle'
    if apk.version!='871_156598' or apk.index[bundle]!='2719b9183d3296a439780b070ce4743c':raise ValueError('Original animation-rule bundle changed')
    original_asset=apk.asset(bundle,'/AppearanceAnimNameCombineRule.asset')
    schemas=NativeSchemas(CODE.parent)
    original,_=schemas.read(original_asset)
    def references(value,field=''):
        if isinstance(value,dict):
            if 'm_FileID' in value and 'm_PathID' in value:
                if field!='m_Script' and any(value.values()):raise ValueError('Unexpected animation-rule asset dependency: '+field)
            else:
                for k,v in value.items():references(v,k)
        elif isinstance(value,list):
            for v in value:references(v,field)
    references(original)
    native=source_header();native.update(fields(dict(original,m_Script=dict(m_FileID=0,m_PathID=0))))
    native['m_Script']=dict(fileID=11500000,guid=hashlib.md5(b'zjcs-profession-ui-AppearanceAnimNameCombineRule').hexdigest(),type=3);native['m_EditorClassIdentifier']=''
    asset=ROOT/'unity/Assets/Official/ProfessionUI/Portrait/AppearanceAnimNameCombineRule.asset'
    asset.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'+yaml.dump({'MonoBehaviour':native},Dumper=Dumper,sort_keys=False,allow_unicode=True,width=1000000),encoding='utf-8',newline='\n')
    meta=asset.with_name(asset.name+'.meta');meta.write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:profession:871_156598:ScriptableConfig/Modules/Appearance/AppearanceAnimNameCombineRule.asset').hexdigest()+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n',encoding='utf-8',newline='\n')
    fixture=CACHE/'typed/player-appearance-validation.json'
    fixture.write_text(json.dumps(dict(path=asset.relative_to(ROOT/'unity').as_posix(),rules=original['mRules']),ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    binding_files=[RUNTIME/(n+'.cs') for n in ('ProfessionPlayerAppearanceBindings','ProfessionAppearancePreviewBindings')];validation=ROOT/'unity/Assets/Game/Editor/ProfessionUI/ProfessionPlayerAppearanceValidation.cs'
    closure=CACHE/'typed/native-bindings-main-dynamic.json';bound=json.loads(closure.read_bytes())
    for name in ('PlayerAppearance','PlayerAnimationAppearance','ProfessionAppearance'):
        row=next(v for k,v in bound['script_bindings'].items() if Path(k).stem==name)
        if row is None or record(ROOT/row['path'])['sha256']!=row['sha256']:raise ValueError('Recheck native script closure before recording '+name)
    closure_copy=CACHE/'typed/player-appearance-binding-closure.json';shutil.copyfile(closure,closure_copy)
    proof=dict(format=1,manifest='871_156598',code_manifest=record(code_manifest),assemblies=assemblies,ports=ports,generator=record(Path(__file__)),
        native_binding_closure=record(closure_copy),
        additional_source=[record(CODE/'AppearanceSystem.Runtime/AppearanceUtils.cs')],
        animation_rules=dict(source=apk.provenance(original_asset),payload_sha256=hashlib.sha256(original_asset.get_raw_data()).hexdigest(),original_fields=original,outputs=[record(asset),record(meta)]),fixture=record(fixture),
        bindings=[record(p) for f in binding_files for p in (f,f.with_name(f.name+'.meta'))],validation=[record(validation),record(validation.with_name(validation.name+'.meta'))],
        operation='Migrate full original PlayerAppearanceInfo, PlayerAppearance, PlayerAnimationAppearance, APlayerAppearanceInfo, ProfessionAppearance, runtime-data override contract, lite callback lifecycle, appearance-event/equipment enums and animation-format rule classes. Scope ProfessionUtils to its complete SetProfessionAppearance method: clone, default weapon overwrite/removal, appended rewards, runtime build, character apply and looping UI animation retain source order. Preserve registration, refresh/animation/load order, exact data identity, runtime object reuse, override restoration, event exception boundaries, cancellation and Clear behavior. Protocol AppearanceData and profession enum cross explicit bindings as object/int without serialization or substitute rules; clone dictionary views address the actual cloned object. Animation-name formatting is scoped to the original GetAnimName and GetFormatId methods; all seven rule records are recovered from the APK without changes. No skeleton SDK implementation is used.',
        validated_scope='Isolated Unity 2022.3.57f1c2 Play Mode passed: self registration/enable/disable and event filtering, released/replaced/late lite callbacks including original Clear behavior, runtime/data identity, override restoration, observer order and exception boundary, animation before loading, cached names/loop/base-profession/sex and formatting; seven original rule records match every field/order and actual native deserialized lookup/format. Profession preview clones before default weapon/reward updates, retains original data, preserves missing-sex defaults and None behavior, and routes the looping UI animation after appearance application. Player/protocol/resource/animation endpoints and dictionary clone are fixtures. Native character rendering and full profession page remain unbound.',
        limitations=['Source data construction, protocol player/lite-info providers, native character rendering and full profession UI are not installed by this application-layer port.'])
    (ROOT/'assets/reference/profession-ui/player-appearance-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Ported original player appearance lifecycle and animation-name rules.')


if __name__=='__main__':build()
