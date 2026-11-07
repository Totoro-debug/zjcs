"""Install the source-verified complete RT closure into the owned Unity fixture."""
import argparse
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from prepare_activity_rt_monster_validation import main as prepare_monster
from prepare_activity_rt_prefab import NAME
from prepare_activity_monster_native import yaml_bytes
from prepare_partner_card_native import guid, sha
from prepare_partner_input_dialog import metadata
from validation_workspace import validation_directory


def install(project, proof, stage, *, entity_id=3706, destination_relative=None, resource_relative=None):
    destination = project / (destination_relative or 'Assets/Official/DailyChallenges/ActivityRtPrefab')
    resource = Path('Resources') / (resource_relative or 'DailyChallenges/ActivityRtPrefab')
    aliases, paths = {}, {}
    for relative, expected in proof['outputs'].items():
        source = stage / relative
        if sha(source.read_bytes()) != expected: raise ValueError('Staged RT closure changed: ' + relative)
        if relative.endswith('.meta'):
            old = guid(source)
            # Preserve the established 3706 identities. Other complete RT
            # closures own their assets, including shared original shaders.
            namespace = 'zjcs:activity-rt-prefab-closure:' + ('' if entity_id == 3706 else str(entity_id) + ':')
            aliases[old] = hashlib.md5((namespace + old).encode()).hexdigest()
            item = relative.removesuffix('.meta')
            name = f'MonsterModel_{entity_id}_RT.prefab'
            paths[old] = resource / (name if item == name else 'Payload/' + item)
    outputs = {}
    for relative in proof['outputs']:
        source = stage / relative
        item = relative.removesuffix('.meta')
        old = guid(stage / (item + '.meta'))
        target = destination / (str(paths[old]) + ('.meta' if relative.endswith('.meta') else ''))
        raw = source.read_bytes()
        if relative.endswith('.meta') or raw.startswith(b'%YAML'):
            text = raw.decode('utf8')
            text = re.sub(r'(?<=guid: )[a-f0-9]{32}', lambda m: aliases.get(m[0], m[0]), text)
            raw = text.encode('utf8')
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(raw)
        outputs[target.relative_to(project).as_posix()] = sha(raw)
    def pointer(row): return dict(row, guid=aliases.get(row['guid'], row['guid']))
    configs = [row for row in proof['dynamic_assets'] if row['path'].endswith('/AppearanceConfig.asset')]
    if not configs:
        return dict(staged_source=proof, outputs=outputs, guid_aliases=aliases,
            scope='Complete original hierarchy with direct atlas textures; rendered validation pending.')
    config, = configs
    script = ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/PartnerNativeDetailActorAssets.cs'
    fields = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0),
        m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0), m_Enabled=1, m_EditorHideFlags=0,
        m_Script=dict(fileID=11500000, guid=guid(metadata(script)), type=3), m_Name=f'Original {entity_id} RT appearance assets',
        m_EditorClassIdentifier='', config=pointer(config['asset']),
        assets=[dict(Path=row['path'], Resource='', Asset=pointer(row['asset'])) for row in proof['dynamic_assets']],
        prefabs=[], textures=[dict(Guid=row['guid'], Resource=paths[row['asset']['guid']].relative_to('Resources').with_suffix('').as_posix())
                              for row in proof['texture_addresses']],
        atlases=[pointer(row) for row in proof['native_atlases']], atlasSourceGuids=[])
    catalog = destination / resource / 'AppearanceAssets.asset'
    catalog.write_bytes(yaml_bytes({11400000: dict(MonoBehaviour=fields)}))
    metadata(catalog)
    for path in (catalog, Path(str(catalog) + '.meta')): outputs[path.relative_to(project).as_posix()] = sha(path.read_bytes())
    return dict(staged_source=proof, outputs=outputs, guid_aliases=aliases,
                scope='Complete original hierarchy installed in owned fixture; rendered validation pending.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true', help='Install the validated native closure in the game project.')
    parser.add_argument('--validation-log', type=Path, help='Successful complete rendered fixture log in the owned workspace.')
    args = parser.parse_args()
    if args.install:
        install_validated(validation_directory(), args.validation_log)
        return
    prepare_monster()
    work = validation_directory()
    proof = json.loads((work / NAME).read_bytes())
    source = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/BattleNativeMonsterRenderer.Prefab.cs'
    for path in (source, metadata(source)):
        target = work / 'unity' / path.relative_to(ROOT / 'unity')
        target.write_bytes(path.read_bytes())
    path = work / 'unity/Assets/Game/Editor/Zjcs.Editor.asmdef'
    text = path.read_text('utf8')
    if '"Zjcs.Official.BattleSpinePlayables"' not in text:
        anchor = '    "Zjcs.Official.ProfessionUI",'
        if text.count(anchor) != 1: raise ValueError('Reinspect fixture assembly references')
        path.write_text(text.replace(anchor, anchor + '\n    "Zjcs.Official.BattleSpinePlayables",'), 'utf8', newline='\n')
    result = install(work / 'unity', proof, work / 'rt-prefab-assets')
    (work / 'rt-prefab-install.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8')
    print('Installed complete original 3706 RT hierarchy and appearance requests in owned fixture.', flush=True)


def install_validated(work, validation_log):
    if not validation_log or not validation_log.resolve().is_relative_to(work.resolve()):
        raise ValueError('Select the successful rendered log in the owned workspace')
    log = validation_log.read_text('utf8', errors='replace')
    if any(value not in log for value in ('[Battle hosts] Complete RT hierarchy:',
                                          'Original TempRTUI passed:', '[Skills acceptance] passed')):
        raise ValueError('Validate the complete rendered target before installing its assets')
    source = json.loads((work / NAME).read_bytes())
    if source['generator_sha256'] != sha((ROOT / 'scripts/prepare_activity_rt_prefab.py').read_bytes()):
        raise ValueError('Rebuild changed native RT extraction')
    for path, expected in source['inputs'].items():
        if sha((ROOT / path).read_bytes()) != expected: raise ValueError('Original RT input changed: ' + path)
    project = work / 'unity'
    relative = Path('Assets/Official/DailyChallenges/ActivityRtPrefab')
    target = ROOT / 'unity' / relative
    receipt = ROOT / 'assets/reference/daily-challenges/activity-rt-prefab-source-manifest.json'
    old = json.loads(receipt.read_bytes()).get('outputs', {}) if receipt.exists() else {}
    installed = {}
    candidates = [project / relative, *sorted((project / relative).rglob('*'))]
    files = [path for path in candidates if path.is_file()]
    files += [Path(str(project / relative) + '.meta')]
    for path in files:
        destination = ROOT / 'unity' / path.relative_to(project)
        key = destination.relative_to(ROOT).as_posix()
        if destination.exists() and sha(destination.read_bytes()) != old.get(key):
            raise ValueError('Preserve existing RT prefab output: ' + key)
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(path.read_bytes()); installed[key] = sha(path.read_bytes())
    bindings = {}
    paths = list(source['script_bindings'])
    paths += ['unity/Assets/Game/Runtime/Offline/ProfessionUI/BattleNativeMonsterRenderer.' + name + '.cs'
              for name in ('Prefab', 'Animator')]
    paths += ['scripts/prepare_activity_rt_prefab_validation.py',
              'unity/Assets/Game/Editor/HomeBattleHostPlayModeAcceptance.ActivityRtUI.cs']
    for relative in paths: bindings[relative] = sha((ROOT / relative).read_bytes())
    result = dict(source=source, outputs=installed, runtime_bindings=bindings,
        native_hierarchy='All 373 original objects and 22 original application components; one additional shared native mesh adapter.',
        validation='Rendered isolated Unity D3D11 Play Mode: original RT camera/geometry, Animator changes, 20 follower references and bone positions, particles, four Low atlas requests, seat ownership and resource disposal. Full production activity page and bloom output pending.',
        installer_sha256=sha(Path(__file__).read_bytes()))
    receipt.write_text(json.dumps(result, ensure_ascii=False, separators=(',', ':')) + '\n', 'utf8', newline='\n')
    print('Installed complete original 3706 RT prefab:', len(installed), 'native outputs.', flush=True)


if __name__ == '__main__': main()
