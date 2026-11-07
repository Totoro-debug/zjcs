"""Recover the official 3803 result model through the shared native RT path."""
import json
import hashlib
import argparse
from pathlib import Path

from official_apk import ROOT
from prepare_battle_live_monsters import build as recover, sha, encoded
from prepare_battle_live_monster_assets import build as assets
from prepare_activity_rt_prefab import ActivityRtPrefabBinding, NAME
from prepare_activity_rt_prefab_validation import install
from prepare_partner_card_native import PartnerCardBinding
from validation_workspace import validation_directory

MODELS = ROOT / 'unity/Assets/Official/Combat/DemonResultRtModels'


class DemonRtPrefabBinding(ActivityRtPrefabBinding):
    def __init__(self, work):
        # The shared RT path consumes no partner skeleton or illustration data.
        self.entity_id = 3803
        self.native_assets_root = MODELS
        self.resource_path = 'Combat/DemonResultRtModels/catalog'
        self.actor_rows, self.native_actors, self.native_atlases = {}, {}, {}
        self.actor_roots, self.dynamic_assets, self.texture_addresses = [], [], []
        self.texture_objects = {}
        self.original_pointers, self.prefab_object_pointers, self.prefab_references = {}, {}, {}
        self.original_objects_by_guid, self.animation_scripts, self.animation_root_paths = {}, {}, []
        PartnerCardBinding.__init__(self, work)


def main(install_staged=False):
    work = validation_directory()
    if install_staged:
        proof = json.loads((work / NAME).read_bytes())
        for relative, expected in proof['inputs'].items():
            if sha((ROOT / relative).read_bytes()) != expected:
                raise ValueError('Staged RT source changed: ' + relative)
        for row in proof['script_bindings'].values():
            path = ROOT / row['path']
            if sha(path.read_bytes()) != row['sha256'] or sha(Path(str(path) + '.meta').read_bytes()) != row['meta_sha256']:
                raise ValueError('Staged RT script changed: ' + str(path))
        finish(work, proof)
        return
    recover((), rt_roles=(3803,))
    catalog = json.loads((work / 'live-monsters/catalog.json').read_bytes())
    model, = catalog['models']
    if (model['entityId'] != 3803 or model['rtPrefab']['path'] !=
            'Assets/Prefabs/Appearance/Monster/3803/MonsterModel_3803_RT.prefab'
            or model['rtPrefab']['appearance'] is not None or model['rtPrefab']['textureLoader'] is not None):
        raise ValueError('Reinspect the actual demon RT prefab and texture resolution')
    proof = json.loads((ROOT / 'assets/reference/battle-animation/live-monsters-source-manifest.json').read_bytes())
    relative = next(name for name in proof['outputs'] if '/textures/' in name and name.endswith('.asset'))
    template = ROOT / relative
    if sha(template.read_bytes()) != proof['outputs'][relative]:
        raise ValueError('The shared native texture serializer template changed')
    text = template.read_text('utf8')
    if text.count('--- !u!28 &2800000\n') != 1:
        raise ValueError('Reinspect the common native texture serializer object ID')
    (work / 'monster-texture-template.asset').write_text(text.replace(
        '--- !u!28 &2800000\n', '--- !u!28 &1\n'), 'utf8', newline='\n')
    assets(destination=MODELS, resource_path='Combat/DemonResultRtModels/catalog',
        receipt_path=ROOT / 'assets/reference/battle-animation/demon-result-rt-model-source-manifest.json')
    binding = DemonRtPrefabBinding(work)
    try: binding.build()
    finally: binding.recovery.apk.archive.close()
    proof = json.loads((work / NAME).read_bytes())
    finish(work, proof)


def finish(work, proof):
    result = install(ROOT / 'unity', proof, work / 'rt-prefab-assets', entity_id=3803,
        destination_relative='Assets/Official/Combat/DemonResultRtPrefab',
        resource_relative='Combat/DemonResultRtPrefab')
    destination = ROOT / 'unity/Assets/Official/Combat/DemonResultRtPrefab'
    for folder in [destination, *[path for path in destination.rglob('*') if path.is_dir()]]:
        metadata = Path(str(folder) + '.meta')
        if not metadata.exists():
            metadata.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(
                ('zjcs:demon-result-rt-folder:' + folder.relative_to(ROOT).as_posix()).encode()).hexdigest() +
                '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
        result['outputs'][metadata.relative_to(ROOT / 'unity').as_posix()] = sha(metadata.read_bytes())
    result['implementation'] = {relative: sha((ROOT / relative).read_bytes()) for relative in (
        'unity/Assets/Game/Runtime/Offline/ProfessionUI/BattleNativeMonsterRenderer.Prefab.cs',
        'unity/Assets/Game/Runtime/Offline/ProfessionUI/BattleNativeMonsterMaterialOverrides.cs',
        'unity/Assets/Game/Runtime/Offline/ProfessionUI/BattleNativeMonsterMeshCopy.cs',
        'unity/Assets/Game/Runtime/Official/ActivityBattleUI/TempRTUI.cs')}
    source = ROOT / '.tools/source-packages/current/884_156692/official-code/client-source/Spine.Extension/CustomRenderExistingMesh.cs'
    result['application_sources'] = {source.relative_to(ROOT).as_posix(): sha(source.read_bytes())}
    result['installation_inputs'] = {'scripts/prepare_activity_rt_prefab_validation.py':
        sha((ROOT / 'scripts/prepare_activity_rt_prefab_validation.py').read_bytes())}
    result['generator_sha256'] = sha(Path(__file__).read_bytes())
    (ROOT / 'assets/reference/battle-animation/demon-result-rt-prefab-source-manifest.json').write_bytes(encoded(result))
    print('Installed original demon RT model and complete prefab dependency graph', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install-staged', action='store_true')
    main(parser.parse_args().install_staged)
