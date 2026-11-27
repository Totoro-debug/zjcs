"""Restore typed current status entities and their actual managed view registry."""
import json
import re
import struct
from Crypto.Hash import MD4

from official_source import OfficialSource, ROOT
from battle_native_script_bindings import shared_bindings
from current_battle_entity_views import CurrentBattleEntityViews
from prepare_battle_status_orbit_assets import build
from prepare_battle_scene_appearance_runtime import identity, write_changed


def build_current_status_graphs(*, family, catalog_name, cache_name, entities, source_wrappers, generator,
                                entity_paths=None, script_bindings=None, external_assets=None):
    source = OfficialSource()
    cache = source.prepared_path / cache_name
    exported = cache / 'native-export/ExportedProject/Assets'
    code = cache.parent / 'official-code'
    roots = (dict(entity_paths) if entity_paths is not None else
             {key: f'Prefabs/EcEntity/Fight/FightSkill/{key}/{key}.prefab' for key in entities})
    assert roots.keys() == set(entities), 'Status prefab paths must cover exactly the selected entities'
    views = CurrentBattleEntityViews(exported, roots.values())
    bindings = shared_bindings()
    bindings['MAPPED'].update(script_bindings or {})
    bindings['MAPPED'] = {key: row for key, row in bindings['MAPPED'].items()
        if any((code / folder / (row[1] + '.cs')).is_file()
            for folder in ('client-source', 'game-framework-source/GameFramework'))}
    bindings['source_scripts'] = {key: row for key, row in bindings['source_scripts'].items()
        if (code / row.get('source_root', 'client-source') / row['source']).is_file()}
    for folder, name, empty in source_wrappers:
        key = struct.unpack('<i', MD4.new(('s\0\0\0' + name).encode()).digest()[:4])[0]
        original = code / 'client-source' / (name + '.cs')
        if empty and not re.search(r'public class ' + name + r'\s*:\s*[^{}]+\{\s*\}\s*$',
                original.read_text('utf-8-sig')):
            raise ValueError('Review changed original status wrapper: ' + name)
        bindings['MAPPED'].pop(key, None)
        bindings['source_scripts'][key] = dict(name=name, namespace='',
            target_namespace='Zjcs.Official.' + folder, source=name + '.cs',
            runtime=f'unity/Assets/Game/Runtime/Official/{folder}/{name}.cs')
    bindings['reference_scripts'] = {key: row for key, row in bindings['reference_scripts'].items()
        if (exported / row['native_asset']).is_file()}
    target = ROOT / f'unity/Assets/Official/Combat/{family}/Resources/Combat/{family}'
    data = ROOT / f'unity/Assets/Game/Resources/Combat/{catalog_name}.json'
    receipt = ROOT / f'assets/reference/battle-animation/{catalog_name}-source-manifest.json'
    build(DEST=target, DATA=data, RECEIPT=receipt, ROOTS={}, graph_roots=roots, **bindings,
        family=family, shader_prefix=family.removesuffix('Graphs') + '_', generator=generator,
        native_manifest=source.version, native_cache=cache, native_export_name='native-export',
        native_preprocess=views.preprocess, external_assets=external_assets,
        limits=['Owning skill separately verifies current cultivation, complete rules, rendered Windows gameplay and checkpoint replay.'])
    assert all(count == 2 for count in views.counts.values()), views.counts
    proof = json.loads(receipt.read_bytes())
    proof['current_managed_views'] = dict(registrations=views.registrations, pointers=views.pointer_bindings,
        schema_inputs=views.schema_inputs, schema_tools=views.schema_tools,
        runtime_receipt=identity(ROOT / 'assets/reference/battle-animation/current-status-ec-view-source-manifest.json'))
    proof['binding_registry'] = [identity(ROOT / 'scripts' / name) for name in
        ('current_battle_native_status_graphs.py', 'current_battle_entity_views.py',
         'current_component_schemas.py', 'battle_native_script_bindings.py')]
    write_changed(receipt, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
