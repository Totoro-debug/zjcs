"""Follow original Spine asset pointers, including assets embedded in LegionMap."""
import hashlib
import struct
from pathlib import Path
from UnityPy.classes.PPtr import PPtr
from official_apk import ROOT


def pointers(obj, start=32):
    raw = obj.get_raw_data()
    for offset in range(start, len(raw) - 11, 4):
        file_id, path_id = struct.unpack_from('<iq', raw, offset)
        if not path_id or not 0 <= file_id <= len(obj.assets_file.externals):
            continue
        try:
            yield PPtr(m_FileID=file_id, m_PathID=path_id, assetsfile=obj.assets_file).deref()
        except (KeyError, FileNotFoundError, ValueError, AttributeError):
            continue


def skeleton_job(exporter, asset, skin=''):
    definition = asset.read(check_read=False)
    raw = asset.get_raw_data()
    offset = (32 + len(definition.m_Name.encode()) + 3) & ~3
    count = struct.unpack_from('<i', raw, offset)[0]
    assert 0 < count < 100
    identity = asset.assets_file.name + ':' + str(asset.path_id) + ':' + skin
    if identity in exporter.skeleton_jobs:
        return identity
    folder = ROOT / '.tools/source-packages/guild-skeletons' / hashlib.sha256(identity.encode()).hexdigest()[:16]
    folder.mkdir(parents=True, exist_ok=True)
    job = dict(skin=skin, textures={}, source=exporter.apk.provenance(asset),
               scale=struct.unpack_from('<f', raw, offset + 4 + 12 * count)[0])
    skeleton = PPtr(m_FileID=struct.unpack_from('<i', raw, offset+8+12*count)[0],
                   m_PathID=struct.unpack_from('<q', raw, offset+12+12*count)[0], assetsfile=asset.assets_file).deref()
    assert skeleton.type.name == 'TextAsset'
    target = folder / (skeleton.read().m_Name + '.bytes')
    target.write_bytes(skeleton.read().m_Script.encode('utf8', 'surrogateescape'))
    job['skeleton'] = str(target)
    for i in range(count):
        atlas = PPtr(m_FileID=struct.unpack_from('<i',raw,offset+4+i*12)[0],
                    m_PathID=struct.unpack_from('<q',raw,offset+8+i*12)[0], assetsfile=asset.assets_file).deref()
        for ptr in pointers(atlas):
            if ptr.type.name == 'TextAsset':
                text = ptr.read()
                if '.atlas' not in text.m_Name:
                    continue
                target = folder / text.m_Name
                target.write_bytes(text.m_Script.encode('utf8', 'surrogateescape'))
                job['atlas'] = str(target)
            elif ptr.type.name == 'Material':
                material = ptr.read()
                for _, texture in material.m_SavedProperties.m_TexEnvs:
                    if texture.m_Texture.path_id:
                        tex = texture.m_Texture.read()
                        job['textures'][tex.m_Name+'.png'] = exporter.texture(texture.m_Texture)
    assert job.get('atlas') and job['textures'], definition.m_Name
    exporter.skeleton_jobs[identity] = job
    return identity
