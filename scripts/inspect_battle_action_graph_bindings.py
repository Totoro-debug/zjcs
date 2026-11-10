"""Resolve selected original skill-movement entities and their Playable pointers."""
import argparse
import hashlib
import json
import re
from collections import defaultdict

from UnityPy.classes.PPtr import PPtr

from current_component_schemas import CurrentSchemas
from inspect_battle_skill_graph_bindings import EXPORT, EXPORT_RECEIPT, ROOT
from official_apk import Apk
from prepare_battle_scene_appearance_runtime import identity


def inspect(entity_ids):
    receipt = json.loads(EXPORT_RECEIPT.read_text(encoding='utf-8'))
    verified = {row['path']: row['sha256'] for row in receipt['outputs']}
    schemas = CurrentSchemas()
    entries = []
    prefabs = defaultdict(list)
    for prefab in EXPORT.rglob('*.prefab'):
        prefabs[prefab.name].append(prefab)

    def checked(path):
        key = path.relative_to(EXPORT.parents[5]).as_posix()
        if hashlib.sha256(path.read_bytes()).hexdigest() != verified[key]:
            raise ValueError(f'Original movement export changed: {path}')
        return identity(path)

    with Apk.current() as apk:
        if apk.version != '884_156692':
            raise ValueError('Review a changed original movement source version.')
        for entity_id in entity_ids:
            paths = prefabs[f'{int(entity_id)}.prefab']
            if len(paths) != 1:
                raise ValueError(f'Expected one original movement entity {entity_id}: {paths}')
            path = paths[0]
            native = checked(path)
            sidecar = path.with_name(path.name + '.meta')
            native_meta = checked(sidecar)
            bundle = re.search(r'(?m)^  assetBundleName: (.+)$',
                               sidecar.read_text(encoding='utf-8-sig'))[1] + '.bundle'
            original = apk.asset(bundle, '/' + path.name)
            if original.read().m_Name != str(entity_id):
                raise ValueError(f'Original movement identity differs: {entity_id}')
            views = []
            for reference in original.read().m_Component:
                component = reference.component.deref()
                if component.type.name != 'MonoBehaviour':
                    continue
                script = component.read(check_read=False).m_Script.read()
                if script.m_ClassName != 'MyFightActionSkillMove':
                    continue
                fields = schemas.read(component)
                bindings = {}
                for field in ('myPlayablePrefab', 'BlockPlayablePrefab'):
                    pointer = fields[field]
                    if not pointer['m_PathID']:
                        continue
                    target = PPtr(**pointer, assetsfile=component.assets_file).deref()
                    if target.type.name != 'GameObject':
                        raise ValueError(f'Original movement {field} is not a GameObject.')
                    source = apk.provenance(target)
                    family = re.fullmatch(
                        r'defaultpackage_assets_prefabs_ecentity_fight_fightskill_(\d+)\.bundle',
                        source['bundle'])
                    graph_name = target.read().m_Name + '.prefab'
                    if family is not None:
                        graph = EXPORT / family[1] / graph_name
                    else:
                        # Shared collision graphs retain their shared-bundle identity.
                        candidates = []
                        for candidate in prefabs[graph_name]:
                            candidate_meta = candidate.with_name(candidate.name + '.meta')
                            bundle_name = re.search(r'(?m)^  assetBundleName: (.+)$',
                                candidate_meta.read_text(encoding='utf-8-sig'))[1] + '.bundle'
                            if bundle_name == source['bundle']:
                                candidates.append(candidate)
                        if len(candidates) != 1:
                            raise ValueError(f'Resolve one original movement graph: {entity_id}/{field}: {source}')
                        graph = candidates[0]
                    graph_meta = graph.with_name(graph.name + '.meta')
                    checked(graph)
                    checked(graph_meta)
                    exported_bundle = re.search(r'(?m)^  assetBundleName: (.+)$',
                                                graph_meta.read_text(encoding='utf-8-sig'))[1] + '.bundle'
                    if exported_bundle != source['bundle']:
                        raise ValueError(f'Original movement graph bundle differs: {entity_id}/{field}')
                    guid = re.search(r'(?m)^guid: ([a-f0-9]{32})$',
                                     graph_meta.read_text(encoding='utf-8-sig'))[1]
                    native_pointer = re.search(rf'(?m)^  {field}: \{{fileID: (-?\d+), '
                                               rf'guid: {guid}, type: \d+\}}$',
                                               path.read_text(encoding='utf-8-sig'))
                    if native_pointer is None:
                        raise ValueError(f'Original and exported movement pointers differ: {entity_id}/{field}')
                    bindings[field] = dict(path=graph.relative_to(EXPORT).as_posix(),
                        guid=guid, file_id=int(native_pointer[1]), source=source,
                        object_sha256=hashlib.sha256(target.get_raw_data()).hexdigest(),
                        native=identity(graph), native_meta=identity(graph_meta))
                views.append(dict(client_type=script.m_ClassName,
                    enabled=fields['m_Enabled'], source=apk.provenance(component),
                    object_sha256=hashlib.sha256(component.get_raw_data()).hexdigest(),
                    playables=bindings))
            if len(views) != 1:
                raise ValueError(f'Expected one original skill-movement view: {entity_id}')
            entries.append(dict(action_entity_id=int(entity_id),
                entity_prefab=path.relative_to(EXPORT).as_posix(),
                source=apk.provenance(original),
                object_sha256=hashlib.sha256(original.get_raw_data()).hexdigest(),
                native=native, native_meta=native_meta, view=views[0]))
    return dict(manifest='884_156692', native_export_manifest=identity(EXPORT_RECEIPT),
        client_source=identity(ROOT / '.tools/source-packages/current/884_156692/official-code/client-source/MyFightActionSkillMove.cs'),
        actions=entries)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--entity-id', type=int, action='append', required=True)
    args = parser.parse_args()
    print(json.dumps(inspect(args.entity_id), ensure_ascii=False, indent=2))
