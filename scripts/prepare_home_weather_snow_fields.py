"""Recover authored HomeWeatherSnow fields and their exact particle references."""
import argparse
import hashlib
import json
from pathlib import Path

from current_component_schemas import CurrentSchemas
from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from official_source import OfficialSource
from prepare_current_partner_native import identity, pointer


RECEIPT = ROOT / 'assets/reference/home-weather-snow-prefab-source-manifest.json'
BUNDLE = 'defaultpackage_assets_prefabs_home_dynamic_theme_ground.bundle'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def managed_input(source, directory):
    """Use the selected assembly plus verified AOT descriptors, not dummy game code."""
    base = source.prepared_path
    code_path = base / 'code-manifest.json'
    code = json.loads(code_path.read_bytes())
    directory = directory.resolve()
    export_path = directory.parent.parent / 'native-export-source-manifest.json'
    export = json.loads(export_path.read_bytes())
    if (code['manifest'] != source.version or export['manifest'] != source.version
            or not export['completed'] or export['code_manifest_sha256'] != sha(code_path.read_bytes())):
        raise ValueError('Selected schema assembly provenance differs')
    expected = {Path(row['output']).name: row['sha256'] for row in export['type_dependencies']}
    expected.update({name: row['decoded_sha256'] for name, row in code['assemblies'].items()})
    inputs = {}
    for path in sorted(directory.glob('*.dll')):
        actual = sha(path.read_bytes())
        if actual != expected.get(path.name):
            raise ValueError('Unverified schema assembly: ' + str(path))
        inputs[path.relative_to(ROOT).as_posix()] = actual
    if 'Assembly-CSharp.dll' not in {Path(path).name for path in inputs}:
        raise ValueError('The selected original game assembly is missing')
    for path in (code_path, export_path):
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    return inputs


def prepare(directory):
    source = OfficialSource()
    inputs = managed_input(source, directory)
    for relative in ('scripts/current_component_schemas.py', 'scripts/official_unity_schema.py',
                     'scripts/official_metadata.py', 'scripts/official_apk.py',
                     'scripts/official_bundle_reader.py', 'scripts/official_source.py',
                     'scripts/official_manifest.py'):
        inputs[relative] = sha((ROOT / relative).read_bytes())
    metadata_path = source.prepared_path / 'official-code/global-metadata.dat'
    inputs[metadata_path.relative_to(ROOT).as_posix()] = sha(metadata_path.read_bytes())
    schemas = CurrentSchemas(managed_directory=directory)
    configurations = []
    objects = {}
    with Apk(source.version) as apk:
        assets = read_asset_manifest(source.manifest_bytes('DefaultPackage', apk.archive))
        ground_prefabs = {row['path'].rsplit('/', 1)[-1].removesuffix('.prefab'): row
                          for row in assets if row['bundle'] == BUNDLE and row['path'].endswith('.prefab')}

        def record(obj):
            key = identity(obj)
            objects[key] = dict(type=obj.type.name, raw_sha256=sha(obj.get_raw_data()),
                                source=dict(apk.provenance(obj)))
            return key

        def ancestors(transform):
            chain = []
            while transform is not None:
                value = transform.read()
                chain.append(dict(source=record(transform), game_object=record(value.m_GameObject.deref()),
                    name=value.m_GameObject.read().m_Name,
                    position=[value.m_LocalPosition.x, value.m_LocalPosition.y, value.m_LocalPosition.z],
                    rotation=[value.m_LocalRotation.x, value.m_LocalRotation.y,
                              value.m_LocalRotation.z, value.m_LocalRotation.w],
                    scale=[value.m_LocalScale.x, value.m_LocalScale.y, value.m_LocalScale.z]))
                transform = value.m_Father.deref() if value.m_Father.path_id else None
            return chain

        for obj in apk.objects(BUNDLE):
            if obj.type.name != 'MonoBehaviour':
                continue
            component = obj.read(check_read=False)
            script = component.m_Script.deref()
            descriptor = script.read(check_read=False)
            if (descriptor.m_Namespace, descriptor.m_ClassName) != ('Home.Skin', 'HomeWeatherSnow'):
                continue
            game_object = component.m_GameObject.deref()
            name = game_object.read().m_Name
            if name not in ground_prefabs:
                raise ValueError('Weather component has no selected original prefab: ' + name)
            fields = schemas.read(obj)
            if set(fields) != {'m_GameObject', 'm_Enabled', 'm_Script', 'm_Name',
                               'mAppearEffectDuration', 'mSnowParticleSystems', 'mParticleAppearCurve'}:
                raise ValueError('Bind additional official snow fields: ' + name)
            particles = []
            for raw in fields['mSnowParticleSystems']:
                particle = pointer(raw, obj)
                if particle.type.name != 'ParticleSystem':
                    raise ValueError('Snow reference does not target a ParticleSystem')
                particle_fields = particle.read_typetree()
                owner = particle.read(check_read=False).m_GameObject.deref()
                transform = owner.read().m_Component[0].component.deref()
                chain = ancestors(transform)
                if chain[-1]['source'] != identity(game_object.read().m_Component[0].component.deref()):
                    raise ValueError('Snow particle escaped the original ground prefab: ' + name)
                particles.append(dict(source=record(particle),
                    max_particles=particle_fields['InitialModule']['maxNumParticles'], ancestors=chain))
            configurations.append(dict(prefab=ground_prefabs[name], component=record(obj),
                script=record(script), enabled=fields['m_Enabled'],
                appearance_duration=fields['mAppearEffectDuration'],
                particle_appear_curve=fields['mParticleAppearCurve'], particles=particles))
    if not configurations:
        raise ValueError('The selected official ground bundle contains no snow controllers')
    inputs[Path(__file__).relative_to(ROOT).as_posix()] = sha(Path(__file__).read_bytes())
    result = dict(manifest=source.version, inputs=inputs, objects=objects,
        configurations=sorted(configurations, key=lambda row: row['prefab']['path']),
        scope='Original serialized controller settings and particle/ancestor identities. '
              'Native prefab/material recovery and production scene integration remain separate work.')
    raw = (json.dumps(result, ensure_ascii=False, indent=2) + '\n').encode()
    if RECEIPT.exists() and json.loads(RECEIPT.read_bytes()).get('manifest') != source.version:
        raise ValueError('Do not replace an already migrated source selection implicitly')
    RECEIPT.write_bytes(raw)
    print('Recovered original snow prefab configurations:', len(configurations))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--managed-directory', type=Path, required=True)
    prepare(parser.parse_args().managed_directory)
