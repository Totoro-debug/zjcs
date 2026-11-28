"""Stage native round resources, real NPC portraits and an isolated Play Mode flow."""
import json
from pathlib import Path
import shutil
import argparse
import re

from prepare_battle_movement_effect_validation import prepare, ROOT
from prepare_current_battle_hud import work_directory
from validation_assemblies import stage_runtime_modules


def stage(work):
    work = work_directory(work)
    project = prepare(work)
    runtime = ROOT/'unity/Assets/Game/Runtime'
    proof = json.loads((ROOT/'assets/reference/battle-hud/round-scene-native-source-manifest.json').read_bytes())
    used = set()
    for name in proof['installed']:
        path = ROOT/name
        if path.suffix != '.meta':
            used.update(value.decode() for value in re.findall(rb'm_Script: \{fileID: 11500000, guid: ([a-f0-9]{32})',path.read_bytes()))
    definitions = {re.search(r'(?m)^guid: ([a-f0-9]{32})',Path(str(path)+'.meta').read_text('utf-8'))[1]: json.loads(path.read_bytes())['name']
        for path in runtime.rglob('*.asmdef')}
    roots = {'Zjcs.Official.BattleRoundUI'}
    for row in proof['scripts'].values():
        if row['pointer']['guid'] not in used: continue
        path = ROOT/row['path']
        if not path.is_relative_to(runtime):
            if any(path.is_relative_to(ROOT/folder) for folder in ('unity/Library/PackageCache','unity/Packages','unity/Assets/Spine')):
                continue
            raise ValueError('Unsupported external native script: '+str(path))
        parent = path.parent
        while parent.is_relative_to(runtime):
            definition = next(parent.glob('*.asmdef'),None)
            reference = next(parent.glob('*.asmref'),None)
            if definition:
                roots.add(json.loads(definition.read_bytes())['name']); break
            if reference:
                name = json.loads(reference.read_bytes())['reference']
                roots.add(definitions[name[5:]] if name.startswith('GUID:') else name); break
            parent = parent.parent
        else: raise ValueError('No validation assembly for native script: '+row['path'])
    selected = stage_runtime_modules(project, runtime, roots)
    for name in ('Spine', 'TextMesh Pro'):
        shutil.copytree(ROOT/'unity/Assets'/name, project/'Assets'/name, dirs_exist_ok=True)
    source = ROOT/'unity/Assets/Official/Combat/RoundUI'
    target = project/source.relative_to(ROOT/'unity')
    shutil.copytree(source, target, dirs_exist_ok=True)
    shutil.copyfile(Path(str(source)+'.meta'), Path(str(target)+'.meta'))
    obsolete = project/'Assets/Game/Editor/BattleMovementEffects'
    if not obsolete.resolve().is_relative_to(work): raise ValueError('Unexpected validation editor directory')
    shutil.rmtree(obsolete)
    portraits = project/'Assets/Resources/RoundTest'; portraits.mkdir(parents=True, exist_ok=True)
    for npc_id in (8411,11001,11002):
        path, = (ROOT/'unity/Assets/Official/Combat/HUD/Sprites').glob('npc_icon_'+str(npc_id)+'-*.png')
        shutil.copyfile(path, portraits/('Npc_'+str(npc_id)+'.png'))
        shutil.copyfile(Path(str(path)+'.meta'), portraits/('Npc_'+str(npc_id)+'.png.meta'))
    editor = project/'Assets/Editor'; editor.mkdir(exist_ok=True)
    shutil.copyfile(ROOT/'scripts/fixtures/battle_round_ui_native_validation.cs', editor/'CurrentRoundUiValidation.cs')
    (editor/'RoundUiValidation.asmdef').write_text(json.dumps(dict(name='RoundUiValidation', includePlatforms=['Editor'],
        references=sorted(selected|{'Unity.ugui'})),indent=2),encoding='utf-8')
    print('Prepared current round UI:',len(selected),'assemblies;',project,flush=True)
    return project


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work',type=Path,required=True)
    stage(parser.parse_args().work)
