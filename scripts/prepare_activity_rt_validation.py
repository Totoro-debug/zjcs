#!/usr/bin/env python3
"""Prepare a source-verified RT component fixture in an owned Unity snapshot."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from prepare_partner_input_dialog import metadata
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    project = work / 'unity'
    if not (work / '.zjcs-validation.json').is_file() or not (project / 'Assets').is_dir():
        raise ValueError('Use the owned imported validation snapshot')
    cache = ROOT / '.tools/source-packages/current/884_156692/native-conversion'
    receipt_path = cache / 'native-export-all-source-manifest.json'
    conversion = json.loads(receipt_path.read_bytes())
    if (not conversion['completed'] or conversion['manifest'] != '884_156692'
            or hashlib.sha256((cache / 'source-manifest.json').read_bytes()).hexdigest()
                != conversion['input_manifest_sha256']):
        raise ValueError('The original RT prefab conversion receipt differs')
    exported = cache / conversion['export_directory']
    original = exported / 'ExportedProject/Assets/Prefabs/Appearance/Monster/3706/MonsterIcon_3706.prefab'
    expected = {row['path']: row['sha256'] for row in conversion['outputs']}
    sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
    for path in (original, Path(str(original) + '.meta')):
        if sha(path) != expected[path.relative_to(exported).as_posix()]:
            raise ValueError('Original RT fixture prefab changed')
    raw_image_meta = project / 'Library/PackageCache/com.unity.ugui@1.0.0/Runtime/UI/Core/RawImage.cs.meta'
    raw_guid = re.search(r'guid: ([a-f0-9]{32})', raw_image_meta.read_text('utf8'))[1]
    temp_meta = ROOT / 'unity/Assets/Game/Runtime/Official/ActivityBattleUI/TempRTUI.cs.meta'
    temp_guid = re.search(r'guid: ([a-f0-9]{32})', temp_meta.read_text('utf8'))[1]
    text = original.read_text('utf8')
    changes = {
        'm_Script: {fileID: -1512622697, guid: 06dd3d5416bf43ea368cc39ff96e6d5a, type: 3}':
            'm_Script: {fileID: 11500000, guid: ' + temp_guid + ', type: 3}',
        'm_Script: {fileID: -98529514, guid: d3e719b59ab71ba3f6b398058c866280, type: 3}':
            'm_Script: {fileID: 11500000, guid: ' + raw_guid + ', type: 3}',
        # A component-fixture factory supplies the existing verified 3709 model
        # at runtime. No incomplete 3706 asset is installed in the game project.
        'RTTarget: {fileID: 1709254077376921, guid: 19906396152e78b4e9fc8ca35cff7edf, type: 2}': 'RTTarget: {fileID: 0}',
    }
    for old, new in changes.items():
        if text.count(old) != 1: raise ValueError('Reinspect original RT prefab field: ' + old)
        text = text.replace(old, new)
    target = project / 'Assets/ActivityRtFixture.prefab'
    target.write_text(text, 'utf8', newline='\n')
    metadata(target)
    sources = ['Assets/Game/Runtime/Official/ActivityBattleUI/' + name for name in (
        'SimpleRTRenderPass.cs', 'ISpineRTUI.cs', 'ISpineRTUIManager.cs', 'IUIBloomRequirer.cs',
        'SpineRTUIManager.cs', 'TempRTUI.cs', 'IActivityRtUIBindings.cs', 'Zjcs.Official.ActivityBattleUI.asmdef')]
    sources.append('Assets/Game/Editor/HomeBattleHostPlayModeAcceptance.ActivityRtUI.cs')
    for relative in list(sources):
        source = ROOT / 'unity' / relative
        sources.append(relative + '.meta')
        metadata(source) if relative.endswith('.cs') else None
    for relative in sources:
        source, destination = ROOT / 'unity' / relative, project / relative
        raw = source.read_bytes()
        if not destination.exists() or destination.read_bytes() != raw: destination.write_bytes(raw)
    path = project / 'Assets/Game/Editor/HomeBattleHostPlayModeAcceptance.cs'
    text = path.read_text('utf8')
    branch = '''            if (Environment.GetEnvironmentVariable("ZJCS_ACTIVITY_RT_UI") == "1")
            {
                yield return ExerciseActivityRtUI(output);
                yield break;
            }
'''
    if 'ZJCS_ACTIVITY_RT_UI' not in text:
        anchor = '            if (Environment.GetEnvironmentVariable("ZJCS_ACTIVITY_MONSTERS")'
        if text.count(anchor) != 1: raise ValueError('Reinspect isolated fixture branch')
        path.write_text(text.replace(anchor, branch + anchor), 'utf8', newline='\n')
    path = project / 'Assets/Game/Editor/Zjcs.Editor.asmdef'
    text = path.read_text('utf8')
    reference = '    "Unity.RenderPipelines.Core.Runtime",\n'
    if reference not in text:
        anchor = '    "Unity.TextMeshPro",\n'
        if text.count(anchor) != 1: raise ValueError('Reinspect isolated Editor references')
        path.write_text(text.replace(anchor, anchor + reference), 'utf8', newline='\n')
    proof = ROOT / 'assets/reference/daily-challenges/activity-rt-ui-source-manifest.json'
    data = json.loads(proof.read_bytes())
    data['validation_inputs'] = {p.relative_to(ROOT).as_posix(): sha(p) for p in
        (Path(__file__), original, Path(str(original) + '.meta'), ROOT / 'unity/Assets/Game/Editor/HomeBattleHostPlayModeAcceptance.ActivityRtUI.cs')}
    data['validation_scope'] = 'Original seven-object 3706 icon, with script bindings restored and test-factory target supplied at runtime. The existing original 3709 native world mesh exercises RT pixels and Animator changes. Production 3706 model, attachments, particles and bloom output remain pending.'
    proof.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Prepared original RT component fixture:', target, flush=True)


if __name__ == '__main__': main()
