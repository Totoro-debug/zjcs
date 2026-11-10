"""Install hash-checked current home configuration, predicates and explicit bindings."""
import argparse
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from validation_workspace import validation_directory


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def guid(name):
    return hashlib.md5(('zjcs:home-current:'+name).encode()).hexdigest()


def metadata(name,importer='DefaultImporter'):
    return ('fileFormatVersion: 2\nguid: '+guid(name)+'\n'+importer+
            ':\n  externalObjects: {}\n'+('  mainObjectFileID: 11400000\n' if importer=='NativeFormatImporter' else '')+
            '  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode()


def main():
    parser=argparse.ArgumentParser(__doc__)
    parser.add_argument('--install',action='store_true')
    args=parser.parse_args()
    work=validation_directory()
    configuration=json.loads((work/'home-rules-current-source-manifest.json').read_bytes())
    conditions=json.loads((work/'home-condition-source-manifest.json').read_bytes())
    if configuration['manifest']!=conditions['manifest'] or conditions['manifest']!='884_156692':
        raise ValueError('Selected home versions differ')
    for receipt in (configuration,conditions):
        for path,expected in receipt['source_sha256'].items():
            if sha((ROOT/path).read_bytes())!=expected:
                raise ValueError('Edited selected home source: '+path)
    for receipt,script in ((configuration,'prepare_current_home_rules.py'),(conditions,'prepare_home_condition_runtime.py')):
        if receipt['generator_sha256']!=sha((ROOT/'scripts'/script).read_bytes()):
            raise ValueError('Regenerate edited current home script: '+script)
    payload=(work/'home-rules-current.json').read_bytes()
    code=(work/'home-conditions/HomeConditionRules.cs').read_bytes()
    if sha(payload)!=configuration['output']['sha256'] or sha(payload)!=conditions['configuration_sha256']:
        raise ValueError('Edited current home configuration')
    if sha(code)!=conditions['outputs']['HomeConditionRules.cs']:
        raise ValueError('Edited original home predicates')
    runtime='unity/Assets/Game/Runtime/Official/HomeConditions'
    resources='unity/Assets/Game/Resources/Home/OfficialCurrentHomeRuleAssetCatalog.asset'
    binding='unity/Assets/Game/Runtime/Offline/HomeUI/OfficialCurrentHomeRuleAssetCatalog.cs'
    target=configuration['output']['path']
    asmdef=(json.dumps(dict(name='Zjcs.Official.HomeConditions',rootNamespace='Zjcs.Official.HomeConditions',
        references=['Zjcs.Official.Partners'],autoReferenced=False,noEngineReferences=True),indent=2)+'\n').encode()
    script_meta=('fileFormatVersion: 2\nguid: '+guid('HomeConditionRules.cs')+
        '\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n'
        '  executionOrder: 0\n  icon: {fileID: 0}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode()
    catalog=('''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: '''+guid('OfficialCurrentHomeRuleAssetCatalog.cs')+''', type: 3}
  m_Name: OfficialCurrentHomeRuleAssetCatalog
  m_EditorClassIdentifier:
  configuration: {fileID: 4900000, guid: '''+guid('home-rules-current.json')+''', type: 3}
''').encode()
    outputs={target:payload,target+'.meta':metadata('home-rules-current.json','TextScriptImporter'),
        runtime+'.meta':('fileFormatVersion: 2\nguid: '+guid('HomeConditions')+
            '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode(),
        runtime+'/HomeConditionRules.cs':code,runtime+'/HomeConditionRules.cs.meta':script_meta,
        runtime+'/Zjcs.Official.HomeConditions.asmdef':asmdef,
        runtime+'/Zjcs.Official.HomeConditions.asmdef.meta':metadata('HomeConditions.asmdef','AssemblyDefinitionImporter'),
        binding:(ROOT/binding).read_bytes(),
        binding+'.meta':script_meta.replace(guid('HomeConditionRules.cs').encode(),guid('OfficialCurrentHomeRuleAssetCatalog.cs').encode()),
        resources:catalog,resources+'.meta':metadata('HomeCurrentRuleCatalog','NativeFormatImporter')}
    staged=work/'current-home-installed';staged.mkdir(exist_ok=True)
    receipts=(('home-rules-current-source-manifest.json',configuration),('home-condition-source-manifest.json',conditions))
    previous={}
    for name,_ in receipts:
        path=ROOT/'assets/reference'/name
        if path.is_file():previous.update(json.loads(path.read_bytes()).get('installed_outputs',{}))
    for path,raw in outputs.items():
        selected=staged/path;selected.parent.mkdir(parents=True,exist_ok=True);selected.write_bytes(raw)
        installed=ROOT/path
        if installed.is_file() and path!=binding and sha(installed.read_bytes()) not in (sha(raw),previous.get(path)):
            raise ValueError('Preserve edited home output: '+path)
    if args.install:
        for path,raw in outputs.items():
            installed=ROOT/path;installed.parent.mkdir(parents=True,exist_ok=True);installed.write_bytes(raw)
        for name,receipt in receipts:
            receipt['installed_outputs']={path:sha(raw) for path,raw in outputs.items() if
                (path.startswith(runtime) or path==binding or path==binding+'.meta')==(name=='home-condition-source-manifest.json')}
            receipt['bindings_sha256']={binding:sha(outputs[binding]),
                'unity/Assets/Game/Runtime/Offline/HomeUI/Zjcs.Offline.HomeUI.asmdef':sha((ROOT/'unity/Assets/Game/Runtime/Offline/HomeUI/Zjcs.Offline.HomeUI.asmdef').read_bytes())}
            receipt['validation_sha256']={path:sha((ROOT/path).read_bytes()) for path in
                ('scripts/fixtures/home_condition_validation.cs','scripts/validate_home_conditions.py',
                 'scripts/prepare_home_conditions_validation.py')}
            receipt['installer_sha256']=sha(Path(__file__).read_bytes())
            (ROOT/'assets/reference'/name).write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Prepared current home outputs:',len(outputs),'installed:',args.install)


if __name__=='__main__':
    main()
