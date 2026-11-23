"""Recover selected complete Home weapon prefabs and their native Spine closure."""
import argparse
import hashlib
import json
from pathlib import Path
import uuid
from UnityPy.classes.PPtr import PPtr

from home_native_assets import install_staged_assets, write_staged_assets
from native_spine_assets import NativeSpineBinding
from official_apk import ROOT
from official_manifest import read_asset_manifest
from official_source import OfficialSource
from prepare_current_partner_native import PartnerNativeRecovery, identity
from prepare_partner_card_native import guid, sha


DEST = ROOT / 'unity/Assets/Official/Home/WeaponParts'
RECEIPT = ROOT / 'assets/reference/home-weapon-parts-source-manifest.json'


def prepare(names=None):
    source = OfficialSource()
    cache = ROOT / '.tools/source-packages/current' / source.version / 'home-weapon-rack'
    selection = cache / 'selection.json'
    available = json.loads(selection.read_bytes())
    if available['manifest'] != source.version:
        raise ValueError('Home weapon selection differs from the selected source')
    rows = [row for row in available['weapons'] if names is None or Path(row['path']).stem in names]
    if names is not None and len(rows) != len(set(names)):
        raise ValueError('Select exact current Home weapon prefab names')
    work = ROOT / 'output/validation' / ('home-weapon-parts-' + uuid.uuid4().hex)
    work.mkdir(parents=True)
    (work / '.zjcs-validation.json').write_text(json.dumps(dict(format=1, repository=str(ROOT))), 'utf8')
    recovery = PartnerNativeRecovery(native_cache=cache, export_name='native-export', bundle=rows[0]['bundle'])
    binding = NativeSpineBinding(work, recovery=recovery)
    binding.inputs[selection.relative_to(ROOT).as_posix()] = sha(selection.read_bytes())
    selected = [binding.bind_prefab(row) for row in rows]
    binding.resolve_all()
    manifest = recovery.apk.source.manifest_bytes('DefaultPackage', recovery.apk.archive)
    records = read_asset_manifest(manifest)
    loaders, textures = [], {}

    def original_asset(row, kind):
        obj, = [pointer.deref() for path, pointer in recovery.apk.container(row['bundle'])
                if path.casefold() == row['path'].casefold() and pointer.deref().type.name == kind]
        return obj

    config_row, = [row for row in records if row['path'].endswith('/AppearanceConfig.asset')]
    config_obj = original_asset(config_row, 'MonoBehaviour')
    config = dict(asset=config_row, original=identity(config_obj), pointer=binding.original_asset(config_obj))

    skeletons = [proof for proof in binding.proof.values() if (proof.get('native_path') or '').endswith('_SkeletonData.asset')]
    loader_paths = set()
    for skeleton in skeletons:
        path = Path(skeleton['native_path']).relative_to(recovery.native.relative_to(ROOT)).as_posix()
        loader_paths.add('Assets/' + path.removesuffix('_SkeletonData.asset') + '_TextureLoader_High.asset')
    # Explicit preview loaders are serialized dependencies and can select Low
    # independently of the normal AppearanceRendererLite High path.
    for proof in binding.proof.values():
        path = proof.get('native_path') or ''
        if '_TextureLoader_' in path and path.endswith('.asset'):
            loader_paths.add('Assets/' + Path(path).relative_to(recovery.native.relative_to(ROOT)).as_posix())
    requested_guids = set()
    loader_fields = []
    for loader_path in sorted(loader_paths):
        row, = [row for row in records if row['path'] == loader_path]
        obj = original_asset(row, 'MonoBehaviour')
        native = recovery.native / row['path'].removeprefix('Assets/')
        source_guid = guid(Path(str(native) + '.meta'))
        binding.objects[source_guid] = obj
        pointer = binding.load(source_guid)
        fields = recovery.fields(obj)
        atlas = PPtr(**fields['atlasAsset'], assetsfile=obj.assets_file).deref()
        atlas_proof = binding.proof[identity(atlas)]
        loaders.append(dict(path=loader_path, asset=row, original=identity(obj), pointer=pointer,
            atlas=dict(fileID=11400000, guid=atlas_proof['guid'], type=2)))
        loader_fields.append(fields)
        requested_guids.update({reference['AssetReference']['mAssetGuid']
            for mapping in fields['placeholderMap'] for reference in (mapping['targetTextureReference'], mapping['dyeTextureReference'])
            if reference.get('AssetReference') and reference['AssetReference'].get('mAssetGuid')})
    roots = sorted({record['bundle'] for record in records if record['guid'] in requested_guids})
    texture_cache = cache.parent / ('home-weapon-textures-' + sha('\n'.join(roots).encode())[:12])
    completed = texture_cache / 'all-assets/windows-textures/source-manifest.json'
    if not completed.exists():
        from prepare_current_resource_inputs import prepare as prepare_inputs
        from prepare_current_asset_inventory import build as inventory
        from convert_current_texture_mips import build as convert_textures
        prepare_inputs(texture_cache, roots)
        inventory(texture_cache)
        convert_textures(texture_cache, 2)
    binding.add_texture_cache(texture_cache)
    for fields in loader_fields:
        for mapping in fields['placeholderMap']:
            for name in ('targetTextureReference', 'dyeTextureReference'):
                reference = mapping[name].get('AssetReference')
                texture_guid = reference.get('mAssetGuid') if reference else None
                if not texture_guid or texture_guid in textures:
                    continue
                texture_row, = [record for record in records if record['guid'] == texture_guid]
                texture_obj = original_asset(texture_row, 'Texture2D')
                binding.objects[texture_guid] = texture_obj
                pointer = binding.load(texture_guid)
                textures[texture_guid] = dict(asset=texture_row, original=identity(texture_obj), pointer=pointer)
    binding.resolve_all()
    binding.bind_renderer_names(selected, config_obj)
    # Resource paths retain lazy on-demand ownership. Merely loading a native
    # prefab does not make every atlas page's high-resolution texture resident.
    texture_bindings = []
    for texture_guid, entry in sorted(textures.items()):
        pointer = entry['pointer']
        index, = [i for i, (_, token, _) in enumerate(binding.queue) if token == pointer['guid']]
        old, token, data = binding.queue[index]
        target = Path('Resources/Home/WeaponTextures') / old.name
        binding.queue[index] = target, token, data
        binding.proof[entry['original']]['output'] = target.as_posix()
        texture_bindings.append(dict(guid=texture_guid, resource='Home/WeaponTextures/' + target.stem, **entry))
    catalog_script = ROOT / 'unity/Assets/Game/Runtime/Offline/NativeSpine/NativeSpineAppearanceCatalog.cs'
    for path in (catalog_script, Path(str(catalog_script) + '.meta')):
        binding.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    catalog = dict(fileID=11400000, guid=hashlib.md5(b'zjcs:native-spine:home-weapon-catalog').hexdigest(), type=2)
    catalog_data = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0),
        m_PrefabInstance=dict(fileID=0), m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0),
        m_Enabled=1, m_EditorHideFlags=0, m_Script=dict(fileID=11500000, guid=guid(Path(str(catalog_script) + '.meta')), type=3),
        m_Name='HomeWeaponAppearanceCatalog', m_EditorClassIdentifier='', Config=config['pointer'],
        Assets=[dict(Path=entry['path'], Asset=entry['pointer']) for entry in loaders],
        Textures=[dict(Guid=entry['guid'], Resource=entry['resource']) for entry in texture_bindings],
        Atlases=[entry['atlas'] for entry in loaders])
    binding.queue.append((Path('HomeWeaponAppearanceCatalog.asset'), catalog['guid'], {11400000: dict(MonoBehaviour=catalog_data)}))
    staged = work / 'assets'
    outputs = write_staged_assets(binding.queue, staged)
    previous = json.loads(RECEIPT.read_bytes())['outputs'] if RECEIPT.exists() else {}
    installed = install_staged_assets(work, staged, outputs, DEST, 'native-spine-home-weapons', previous_outputs=previous)
    for filename in ('native_spine_assets.py', 'prepare_home_weapon_parts.py', 'home_native_assets.py', 'prepare_partner_card_native.py'):
        path = ROOT / 'scripts' / filename
        binding.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    RECEIPT.write_text(json.dumps(dict(manifest=source.version, asset_manifest_sha256=sha(manifest),
        inputs=binding.inputs, selections=selected, recovery=recovery.proof, native_scripts=binding.script_sources,
        resources=binding.proof, config=config, loaders=loaders, textures=texture_bindings, catalog=catalog, outputs=installed,
        scope='Selected exact native Home weapon prefabs with real Spine SDK skeletons, animation, atlas, materials and high-resolution texture requests. This does not complete the storage building or the full weapon selection catalog.'),
        ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Bound original native Home weapons:', len(selected), 'prefabs,', len(texture_bindings), 'texture identities,', len(installed), 'files; work', work, flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    selection = parser.add_mutually_exclusive_group(required=True)
    selection.add_argument('--parts', nargs='+')
    selection.add_argument('--all', action='store_true', help='Bind every selected official Home weapon prefab')
    prepare(parser.parse_args().parts)
