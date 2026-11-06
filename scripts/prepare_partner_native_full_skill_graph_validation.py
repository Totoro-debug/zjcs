"""Isolate installed original partner skill prefabs and their exact serialized dependencies."""
import json
from pathlib import Path
import re
import shutil
import subprocess

from official_apk import ROOT
from prepare_partner_native_battle_validation import main as prepare_actor
from validation_assemblies import stage_runtime_modules
from validation_workspace import validation_directory


def main(extra_roots=(), extra_editors=()):
    work = validation_directory()
    if work.parent != ROOT / 'output/validation' or not (work / '.zjcs-validation.json').is_file():
        raise ValueError('Use an owned partner full skill graph workspace')
    project = work / 'native-battle-unity'
    if not project.exists():
        prepare_actor()
    elif not (project / 'Assets/Editor/Validation.asmdef').is_file():
        raise ValueError('Preserve the existing validation project')
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    assembly_roots = ['Zjcs.Presentation.Combat', 'Zjcs.Offline.BattleNativePlayables']
    result = subprocess.run(['rg', '--no-ignore', '--hidden', '--no-heading', '--with-filename', '-g', '*.meta',
        '^guid: [0-9a-f]{32}', 'unity/Assets', 'unity/Packages', 'unity/Library/PackageCache'],
        cwd=ROOT, check=True, capture_output=True)
    guids = {}
    for line in result.stdout.decode('utf8').splitlines():
        relative, guid = line.rsplit(':guid: ', 1)
        source = ROOT / relative[:-5]
        if any(part.endswith('~') for part in source.parts):
            continue
        if guid in guids and guids[guid] != source:
            raise ValueError('Duplicate installed asset identity: ' + guid)
        guids[guid] = source
    graph_data = json.loads((ROOT / 'unity/Assets/Game/Resources/Combat/skill-native-graphs.json').read_bytes())
    partner_data = json.loads((ROOT / 'unity/Assets/Game/Resources/Combat/partner-native-playables.json').read_bytes())
    wanted = {s['skill_entity_id'] for s in partner_data['skills']}
    roots = [ROOT / 'unity/Assets/Official/Combat/SkillGraphRuntime/Resources' / (ref['resource'] + '.prefab')
        for row in graph_data['skills'] if row['skill_entity_id'] in wanted for ref in row['playables']]
    if len(wanted) != 75 or len(roots) != 93:
        raise ValueError('Original partner full graph selection changed')
    roots += list((ROOT / 'unity/Assets/Game/Resources/Combat').glob('*.json'))
    roots += [ROOT / path for path in extra_roots]
    roots.append(ROOT / 'unity/Assets/Game/Resources/Combat/OfficialCombatAssetCatalog.asset')
    roots += [path for path in (ROOT / 'unity/Assets/Official/Combat/NativePartnerAssets/Resources').rglob('*')
        if path.is_file() and path.suffix != '.meta']
    pending, seen, package_refs, unresolved = list(roots), set(), set(), {}
    text_assets = {'.prefab', '.asset', '.mat', '.controller', '.overrideController', '.anim'}
    while pending:
        source = pending.pop()
        if source in seen:
            continue
        seen.add(source)
        if not source.is_file():
            raise FileNotFoundError(source)
        relative = source.relative_to(ROOT / 'unity')
        if relative.parts[0] != 'Assets':
            package_refs.add(relative.as_posix())
            continue
        if source.suffix == '.cs' and source.is_relative_to(runtime):
            parent = source.parent
            while parent.is_relative_to(runtime):
                definitions = list(parent.glob('*.asmdef'))
                if definitions:
                    assembly_roots.append(json.loads(definitions[0].read_bytes())['name'])
                    break
                parent = parent.parent
        destination = project / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        # Existing approved effect runtime dependencies are copied unchanged;
        # native partner skeletons continue to use the independent native actor.
        if source.suffix != '.cs' or not source.is_relative_to(ROOT / 'unity/Assets/Spine'):
            shutil.copy2(source, destination)
            shutil.copy2(Path(str(source) + '.meta'), Path(str(destination) + '.meta'))
        if source.suffix in text_assets:
            for guid in re.findall(rb'guid:\s*([0-9a-f]{32})', source.read_bytes()):
                guid = guid.decode()
                if guid.startswith('0000000000000000'):
                    continue
                if guid in guids:
                    pending.append(guids[guid])
                else:
                    unresolved.setdefault(guid, str(source.relative_to(ROOT)))
    if unresolved:
        (work / 'graph-unresolved-dependencies.json').write_text(json.dumps(unresolved, indent=2), encoding='utf8')
        raise ValueError('Unresolved original partner graph dependencies: count=' + str(len(unresolved))
            + ', sample=' + repr(list(unresolved.items())[:5]))
    allowed = {'Cinemachine', 'UniTask', 'Unity.Newtonsoft.Json', 'Unity.RenderPipelines.Core.Runtime',
        'Unity.RenderPipelines.Universal.Runtime', 'Unity.TextMeshPro', 'Unity.Timeline', 'Unity.ugui',
        'spine-csharp', 'spine-unity'}
    selected = stage_runtime_modules(project, runtime, assembly_roots, allowed_external=allowed)
    shutil.copytree(ROOT / 'unity/Assets/Spine/Runtime', project / 'Assets/Spine/Runtime', dirs_exist_ok=True)
    for name in ('com.unity.cinemachine', 'com.unity.timeline'):
        shutil.copytree(ROOT / 'unity/Packages' / name, project / 'Packages' / name, dirs_exist_ok=True)
    shutil.copy2(ROOT / 'unity/Packages/manifest.json', project / 'Packages/manifest.json')
    editor = project / 'Assets/Editor'
    shutil.copy2(ROOT / 'unity/Assets/Game/Editor/PartnerBattlePlayables/OfficialPartnerNativeSkillGraphProbe.cs', editor)
    for source in extra_editors:
        shutil.copy2(ROOT / source, editor)
    definition = json.loads((editor / 'Validation.asmdef').read_bytes())
    definition['references'] = sorted(set(definition['references']) | {'Zjcs.Combat.Official', 'Zjcs.Presentation.Combat',
        'Zjcs.Offline.BattleNativePlayables', 'Zjcs.Official.StoryPlayableManager', 'Zjcs.Official.StoryAnimation',
        'Zjcs.Offline.Equipment', 'Zjcs.Official.StoryPlayables', 'Zjcs.Native.Summons'})
    (editor / 'Validation.asmdef').write_text(json.dumps(definition), encoding='utf8')
    (project.parent / 'partner-graph-fixture-inputs.json').write_text(json.dumps(dict(
        roots=[str(p.relative_to(ROOT)) for p in roots], asset_count=len(seen),
        modules=sorted(selected), package_refs=sorted(package_refs)), indent=2), encoding='utf8')
    print('Prepared installed original partner skill graphs:', len(roots), 'roots,', len(seen), 'dependencies,', len(selected), 'assemblies', flush=True)


if __name__ == '__main__':
    main()
