"""Decode selected original partner portraits without a Spine runtime.

Only stage lossless data in the owning validation workspace. Installing a
portrait requires its native renderer, original material binding and validation.
"""
import argparse
from collections import Counter
import hashlib
import gzip
import io
import json
from pathlib import Path
import struct

from convert_official_skeletons import WHEEL_SHA256, reader_type
from current_component_schemas import CurrentSchemas
from official_apk import Apk, ROOT
from prepare_current_partner_native import identity, pointer
from prepare_native_character_geometry import atlas_regions
from validation_workspace import validation_directory


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def package(npc_id, scale, skeleton, atlas):
    def block(raw):
        compressed = gzip.compress(raw, mtime=0)
        return struct.pack('<I', len(raw))+hashlib.sha256(raw).digest()+struct.pack('<I', len(compressed))+compressed
    manifest = b'884_156692'
    return (b'ZJCSPNPC'+struct.pack('<II', 1, len(manifest))+manifest
            +struct.pack('<if', npc_id, scale)+block(skeleton)+block(atlas))


def build(work, npc_ids=None):
    selected = json.loads((ROOT/'official-source.json').read_bytes())['active']
    rules_path = ROOT/'unity/Assets/Official/Partners/partner-rules.json'
    raw_rules = rules_path.read_bytes()
    rules = json.loads(raw_rules)
    if selected != '884_156692' or rules['manifest'] != selected:
        raise ValueError('Review the partner portrait source after changing versions')
    # The eight paused collaboration partners are recorded in offline-access.md.
    paused = set(range(11015, 11023))
    rows = [row for row in rules['tables']['npc']
            if row['CanDevelop'] and row['NpcType'] == 4 and row['FullBodyLiveImage'] and row['Id'] not in paused]
    if npc_ids is not None:
        wanted = set(npc_ids)
        rows = [row for row in rows if row['Id'] in wanted]
        if {row['Id'] for row in rows} != wanted:
            raise ValueError('Only current available partners with configured live portraits can be selected')
    apk = Apk.current()
    if apk.version != selected:
        raise ValueError('Portrait source is not the selected partner version')
    manifest = apk.source.manifest('DefaultPackage', apk.archive)
    bundles = {row['name'] for row in manifest['bundles']}
    reader_class, _ = reader_type()
    schemas = None
    objects, sources, results, outputs = {}, {}, [], {}
    destination = work/'native-portraits'
    destination.mkdir(exist_ok=True)

    def fields(obj):
        nonlocal schemas
        try:
            return obj.read_typetree()
        except ValueError:
            if schemas is None:
                schemas = CurrentSchemas()
            return schemas.read(obj)

    def record(obj):
        key = identity(obj)
        if key not in sources:
            sources[key] = dict(source=apk.provenance(obj), payload_sha256=sha(obj.get_raw_data()))
        return key

    def write(relative, raw):
        target = destination/relative
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.is_file() or target.read_bytes() != raw:
            target.write_bytes(raw)
        outputs[relative] = sha(raw)
        return relative

    def text_asset(obj, suffix):
        data = obj.read(check_read=False)
        raw = data.m_Script.encode('utf8', 'surrogateescape')
        key = record(obj)
        return raw, dict(original=key, name=data.m_Name,
                         file=write('raw/'+sha(raw)+suffix, raw), sha256=sha(raw))

    for row in rows:
        address = 'Assets/AppearanceAssets/NpcPortrait/'+row['FullBodyLiveImage']
        folder = row['FullBodyLiveImage'].split('/', 1)[0]
        bundle = 'defaultpackage_assets_appearanceassets_npcportrait_'+folder.lower()+'.bundle'
        if bundle not in bundles:
            raise ValueError('Locate the selected portrait bundle for '+address)
        # Verify the actual indexed container, rather than accepting a matching name.
        matches = [ref.deref() for path, ref in apk.container(bundle) if path.lower() == address.lower()]
        if len(matches) != 1:
            raise ValueError('Expected one exact original portrait address: '+address)
        asset = matches[0]
        if asset.type.name != 'MonoBehaviour':
            raise ValueError('Original portrait address is not its data asset: '+address)
        configuration = fields(asset)
        script = pointer(configuration['m_Script'], asset).read(check_read=False)
        if (script.m_Namespace, script.m_ClassName) != ('Spine.Unity', 'SkeletonDataAsset'):
            raise ValueError('Unexpected original partner portrait data class')
        key = record(asset)
        if key in objects:
            results.append(dict(npcId=row['Id'], address=address, original=key,
                                sharedWithNpc=objects[key], liveOffset=row['LiveOffset'], liveScale=row['LiveScale']))
            continue
        objects[key] = row['Id']
        binary = pointer(configuration['skeletonJSON'], asset)
        raw, skeleton_input = text_asset(binary, '.skel.bytes')
        reader = reader_class()
        skeleton = reader.convert(raw)
        if reader.inp.index != len(raw):
            raise ValueError('Partner portrait skeleton bytes were not fully consumed: '+address)
        skeleton_path = str(row['Id'])+'/skeleton.json'
        skeleton_raw = json.dumps(skeleton, ensure_ascii=False, separators=(',', ':'), allow_nan=False).encode()
        write(skeleton_path, skeleton_raw)
        atlases = []
        for reference in configuration['atlasAssets']:
            atlas = pointer(reference, asset)
            atlas_configuration = fields(atlas)
            atlas_script = pointer(atlas_configuration['m_Script'], atlas).read(check_read=False)
            if (atlas_script.m_Namespace, atlas_script.m_ClassName) != ('Spine.Unity', 'SpineAtlasAsset'):
                raise ValueError('Unexpected original partner portrait atlas class')
            atlas_text = pointer(atlas_configuration['atlasFile'], atlas)
            atlas_raw, atlas_input = text_asset(atlas_text, '.atlas.txt')
            regions = atlas_regions(atlas_raw.decode('utf8'))
            materials = []
            for reference in atlas_configuration['materials']:
                material = pointer(reference, atlas)
                if material.type.name != 'Material':
                    raise ValueError('Original portrait atlas references a non-material')
                material_fields = fields(material)
                shader = pointer(material_fields['m_Shader'], material)
                shader_fields = shader.read(check_read=False)
                shader_name = shader_fields.m_ParsedForm.m_Name
                textures = []
                for property_name, environment in material_fields['m_SavedProperties']['m_TexEnvs']:
                    if not environment['m_Texture']['m_PathID']:
                        continue
                    texture = pointer(environment['m_Texture'], material)
                    if texture.type.name != 'Texture2D':
                        raise ValueError('Bind the original non-Texture2D portrait material input')
                    image = texture.read(check_read=False)
                    pixels = image.image.convert('RGBA')
                    png = io.BytesIO()
                    pixels.save(png, format='PNG')
                    textures.append(dict(property=property_name, original=record(texture), name=image.m_Name,
                        width=image.m_Width, height=image.m_Height, format=int(image.m_TextureFormat),
                        mip_count=image.m_MipCount, color_space=image.m_ColorSpace, readable=image.m_IsReadable,
                        image_count=image.m_ImageCount, dimension=int(image.m_TextureDimension),
                        encoded_sha256=sha(image.get_image_data()),
                        rgba_sha256=sha(pixels.tobytes()), png=write('textures/'+sha(png.getvalue())+'.png',png.getvalue()),
                        filter_mode=int(image.m_TextureSettings.m_FilterMode), aniso=image.m_TextureSettings.m_Aniso,
                        mip_bias=image.m_TextureSettings.m_MipBias,
                        wrap=[int(getattr(image.m_TextureSettings,'m_Wrap'+axis)) for axis in 'UVW']))
                materials.append(dict(original=record(material), fields=material_fields,
                    shader=dict(original=record(shader), name=shader_name), textures=textures))
            atlases.append(dict(original=record(atlas), fields=atlas_configuration, text=atlas_input,
                               regions=regions, materials=materials))
        atlas_path = str(row['Id'])+'/atlases.json'
        write(atlas_path, json.dumps(atlases, ensure_ascii=False, separators=(',', ':'), allow_nan=False).encode())
        flat = [region for atlas in atlases for region in atlas['regions']]
        flat_raw = json.dumps(flat,ensure_ascii=False,separators=(',', ':'),allow_nan=False).encode()
        flat_path = write(str(row['Id'])+'/atlas-regions.json',flat_raw)
        native_path = write(str(row['Id'])+'/portrait.bytes',package(row['Id'],configuration['scale'],skeleton_raw,flat_raw))
        attachments = Counter(attachment.get('type', 'region') for skin in skeleton['skins']
                              for values in skin.get('attachments', {}).values() for attachment in values.values())
        results.append(dict(npcId=row['Id'], address=address, original=key, fields=configuration,
                            liveOffset=row['LiveOffset'], liveScale=row['LiveScale'],
                            binary=skeleton_input, skeleton=skeleton_path, atlases=atlas_path,
                            atlasRegions=flat_path, nativePackage=native_path,
                            counts=dict(**{name:len(skeleton.get(name, [])) for name in
                                           ('bones','slots','skins','animations','ik','transform','path','physics')},
                                        attachments=dict(attachments))))
        print('Decoded original portrait',row['Id'],results[-1]['counts'],flush=True)
    tools = ('scripts/prepare_partner_native_portraits.py','scripts/convert_official_skeletons.py',
             'scripts/prepare_native_character_geometry.py','scripts/current_component_schemas.py')
    proof = dict(manifest=selected, rule_source=dict(path=rules_path.relative_to(ROOT).as_posix(),sha256=sha(raw_rules)),
                 generator_inputs={name:sha((ROOT/name).read_bytes()) for name in tools},
                 decoder=dict(name='SkelToJson',version='1.0.1',wheel_sha256=WHEEL_SHA256,license='MIT',float_rounding=False),
                 objects=sources, portraits=results, outputs=outputs,
                 scope='Lossless current source staging only; native graphics, constraints, mixing, material binding and actual window/Player verification remain required.')
    (work/'partner-native-portraits-source-manifest.json').write_text(
        json.dumps(proof,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Staged',len(results),'current original partner portraits; no runtime installation.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--npc-ids',nargs='+',type=int)
    args = parser.parse_args()
    build(validation_directory(),args.npc_ids)
