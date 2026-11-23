"""Extract exact current scene and default-house slot registration inputs."""
import argparse
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from official_source import OfficialSource
from prepare_current_partner_native import PartnerNativeRecovery, identity
from UnityPy.classes.PPtr import PPtr


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def prepare(directory):
    source = OfficialSource()
    directory = directory.resolve()
    directory.relative_to(ROOT / 'output/validation')
    target = directory / 'entity-runtime-input.json'
    if not directory.is_dir() or target.exists():
        raise ValueError('Use a new owned validation input directory')
    recovery = PartnerNativeRecovery(native_cache=ROOT / '.tools/source-packages/current' / source.version / 'home-storage-native',
        export_name='native-export', bundle='defaultpackage_assets_prefabs_home_dynamic_furniture_storagebox.bundle')
    apk = recovery.apk
    nodes, proofs = {}, {}

    def deref(owner, value):
        return PPtr(**value, assetsfile=owner.assets_file).deref()

    def script_name(obj):
        script = obj.read(check_read=False).m_Script.deref().read()
        return script.m_ClassName

    def proof(obj):
        key = identity(obj)
        proofs[key] = dict(source=apk.provenance(obj), sha256=sha(obj.get_raw_data()), type=obj.type.name)
        return key

    def vector(value, keys='xyz'):
        return {key: getattr(value, key) for key in keys}

    def chain(obj):
        key = identity(obj)
        if key in nodes:
            return key
        value = obj.read()
        go = value.m_GameObject.deref()
        game = go.read()
        parent = value.m_Father.deref() if value.m_Father.path_id else None
        nodes[key] = dict(key=key, name=game.m_Name, active=bool(game.m_IsActive),
            parent=chain(parent) if parent is not None else '',
            position=vector(value.m_LocalPosition), rotation=vector(value.m_LocalRotation, 'xyzw'), scale=vector(value.m_LocalScale))
        proof(obj); proof(go)
        return key

    def transform(game_object):
        matches = [pair.component.deref() for pair in game_object.read().m_Component
            if pair.component.deref().type.name == 'Transform']
        obj, = matches
        return obj

    def slots(owner, values):
        return [dict(key=row['Key'], node=chain(deref(owner, row['Value']))) for row in values['mSerializeBuffer']]

    try:
        bundle = 'defaultpackage_assets_scenes_main.bundle'
        list(apk.container(bundle))
        managers = []
        for obj in apk.env.objects:
            if obj.type.name == 'GameObject' and obj.read().m_Name == 'Home' and apk.provenance(obj)['bundle'] == bundle:
                for pair in obj.read().m_Component:
                    component = pair.component.deref()
                    if component.type.name == 'MonoBehaviour' and script_name(component) == 'HomeEntityManagerMono':
                        managers.append(component)
        manager, = managers
        fields = recovery.fields(manager)
        scene = dict(root=chain(transform(deref(manager, fields['m_GameObject']))),
            furniture=slots(manager, fields['FurnitureSlotDict']), themes=slots(manager, fields['HomeThemeSlotDict']),
            mapping=[dict(key=row['Key'], value=row['Value']) for row in fields['FurnitureCreateMapping']['mSerializeBuffer']])
        proof(manager)
        bundle = 'defaultpackage_assets_prefabs_home_dynamic_theme_house.bundle'
        house, = [pointer.deref() for path, pointer in apk.container(bundle)
            if path.casefold() == 'assets/prefabs/home/dynamic/theme/house/house.prefab'
            and pointer.deref().type.name == 'GameObject']
        components = [pair.component.deref() for pair in house.read().m_Component]
        by_type = {script_name(obj): obj for obj in components if obj.type.name == 'MonoBehaviour'}
        provider = by_type['HomeEntityDataProvider']
        data = []
        authorings = []
        for pointer in recovery.fields(provider)['mDataList']:
            obj = deref(provider, pointer)
            name = script_name(obj)
            data.append(name); proof(obj)
            if name == 'HomeBindingAuthoring':
                raw = recovery.fields(obj)
                authorings.append(json.dumps({key: raw[key] for key in ('Profile', 'Units', 'Spaces', 'Channels')}, ensure_ascii=False))
        slot_data = by_type['HomeSlotEntityData']
        house_data = dict(root=chain(transform(house)), furniture=slots(slot_data, recovery.fields(slot_data)['mFurnitureDict']),
            data_types=data, authoring_json=authorings)
        proof(house); proof(provider); proof(slot_data)
        path = ROOT / '.tools/source-packages/current' / source.version / 'official-code/client-source/HomeEntityManagerMono.cs'
        source_paths = [path, path.with_name('HomeSlotEntityData.cs'), Path(__file__)]
        result = dict(manifest=source.version, scene=scene, house=house_data, nodes=list(nodes.values()), source_objects=proofs,
            inputs={p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in source_paths},
            scope='Original scene/default-house transform chains and registration fields only; fixture construction is separate from native model/gameplay migration.')
        target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Prepared original home entity runtime inputs:', target, '; scene slots', len(scene['furniture']), '; house slots', len(house_data['furniture']))
    finally:
        apk.reader.close()


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--validation-dir', required=True, type=Path)
    prepare(parser.parse_args().validation_dir)
