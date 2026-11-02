"""Prepare an isolated native dialogue-entry Play Mode fixture."""
import json
import hashlib
import argparse
from pathlib import Path
import shutil

from official_apk import ROOT, Apk
from official_tables import Constructors
from npc_travel_language import language
from validation_workspace import validation_directory


def prepare(project_name='dialogue-unity'):
    work = validation_directory()
    if work == ROOT / 'output/validation' or not (work / '.zjcs-validation.json').is_file():
        raise ValueError('Create an owned validation workspace')
    if not project_name.replace('-', '').isalnum(): raise ValueError('Use one validation project name')
    project = work / project_name
    if project.exists():
        raise ValueError('Preserve existing validation project')
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    modules = {json.loads(p.read_bytes())['name']: p for p in runtime.rglob('*.asmdef')}
    pending, selected, external = ['Zjcs.Official.PartnerRosterUI'], set(), set()
    while pending:
        name = pending.pop()
        if name in selected: continue
        if name not in modules:
            external.add(name)
            continue
        selected.add(name)
        pending.extend(json.loads(modules[name].read_bytes()).get('references', []))
    if external - {'Unity.ugui', 'UnityEngine.UI', 'Unity.TextMeshPro', 'UniTask'}:
        raise ValueError('Inspect new native validation dependencies: ' + repr(external))
    for name in selected:
        folder = modules[name].parent
        for path in folder.rglob('*'):
            if not path.is_file() or path.suffix not in ('.cs', '.meta', '.asmdef'): continue
            # A parent assembly directory may contain other, separate modules.
            parent = path.parent
            while parent != folder and not list(parent.glob('*.asmdef')): parent = parent.parent
            if parent != folder: continue
            target = project / 'Assets/Game/Runtime' / path.relative_to(runtime)
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, target)
    editor = project / 'Assets/Editor'
    editor.mkdir(parents=True)
    shutil.copy2(ROOT / 'scripts/fixtures/partner_dialogue_entry_validation.cs', editor / 'PartnerDialogueEntryValidation.cs')
    (editor / 'Validation.asmdef').write_text(json.dumps(dict(name='PartnerDialogueEntryValidation', includePlatforms=['Editor'],
        references=['Unity.ugui', 'Unity.TextMeshPro', 'Zjcs.Official.PartnerRosterUI', 'Zjcs.Official.WorldUI',
                    'Zjcs.Official.PartnersUI', 'Zjcs.Official.ProfessionUI'])), 'utf8', newline='\n')
    installed = json.loads((ROOT / 'assets/reference/partner-dialogue-entry-native-source-manifest.json').read_bytes())
    for name, expected in installed['installed_output_sha256'].items():
        source = ROOT / name
        if hashlib.sha256(source.read_bytes()).hexdigest() != expected: raise ValueError('Installed dialogue asset changed: ' + name)
        if '/Runtime/' in name:
            target = editor / source.name
        else:
            target = project / source.relative_to(ROOT / 'unity')
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
    recovered = json.loads((work / 'partner-native/NpcPanelDialogueItem.json').read_bytes())
    entry_root = json.loads((work / 'partner-dialogue-entry-native-source-manifest.json').read_bytes())['roots']['NpcPanelDialogueEntry']['fileID']
    documents = recovered['documents']
    entry_transform = next(iter(documents[str(documents[str(entry_root)]['GameObject']['m_Component'][0]['component']['fileID'])].values()))
    parent = next(iter(documents[str(entry_transform['m_Father']['fileID'])].values()))
    components = {value['component']['fileID'] for value in documents[str(parent['m_GameObject']['fileID'])]['GameObject']['m_Component']}
    group, = [value for value in recovered['scripts'] if value['nativeFileID'] in components and value['type'] == 'VerticalLayoutGroup']
    fields = documents[str(group['nativeFileID'])]['MonoBehaviour']
    (editor / 'dialogue-parent-layout.json').write_text(json.dumps({key: value for key, value in fields.items()
        if key not in ('m_Script', 'm_GameObject', 'm_Name')}), 'utf8')
    shutil.copytree(ROOT / 'unity/Assets/TextMesh Pro', project / 'Assets/TextMesh Pro')
    shutil.copy2(ROOT / 'unity/Assets/Official/World/Map/12/UI/Team/FZY4K.ttf', editor / 'FZY4K.ttf')
    apk = Apk.current()
    obj, = [value for value in apk.objects('defaultpackage_assets_config_luban.bundle')
            if value.type.name == 'TextAsset' and value.read().m_Name.rsplit('/', 1)[-1] == 'npc_dialogue.g']
    raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
    rows = Constructors(ROOT / '.tools/source-packages/current' / apk.version / 'official-code').table(raw, 'TbNpcDialogueInfo')
    row = next(value for value in rows if value['Node'] == 1)
    key = f"npc_dialogue_{row['ClassId']}_1_text"
    texts, sources = language([key])
    (work / 'dialogue-entry-validation-inputs.json').write_text(json.dumps(dict(manifest=apk.version,
        table=dict(source=apk.provenance(obj), sha256=hashlib.sha256(raw).hexdigest()), language=sources)), 'utf8')
    (editor / 'dialogue-text.json').write_text(json.dumps(dict(id=row['ClassId'], key=key, text=texts[key]),
        ensure_ascii=False), 'utf8', newline='\n')
    packages = project / 'Packages'
    packages.mkdir()
    shutil.copytree(ROOT / 'unity/Packages/com.cysharp.unitask', packages / 'com.cysharp.unitask')
    dependencies = {'com.unity.ugui': '1.0.0', 'com.unity.textmeshpro': '3.0.6', 'com.cysharp.unitask': 'file:com.cysharp.unitask'}
    for name in ('ui', 'imgui', 'animation', 'audio', 'jsonserialize', 'screencapture', 'director', 'assetbundle', 'physics', 'physics2d', 'particlesystem'):
        dependencies['com.unity.modules.' + name] = '1.0.0'
    (packages / 'manifest.json').write_text(json.dumps(dict(dependencies=dependencies)), 'utf8')
    settings = project / 'ProjectSettings'
    settings.mkdir()
    shutil.copy2(ROOT / 'unity/ProjectSettings/ProjectVersion.txt', settings)
    print('Prepared original dialogue entry and', len(selected), 'runtime assemblies:', project, flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-name', default='dialogue-unity')
    prepare(parser.parse_args().project_name)
