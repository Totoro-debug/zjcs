"""Restore packed official minimap Sprite geometry for every native asset catalog."""
import copy
from prepare_current_partner_native import identity
from prepare_unity_map13_native_sprite_yaml import fields as sprite_fields, source_header
from UnityPy.helpers.MeshHelper import MeshHandler


def restore_sprite(binding, source_guid, result):
    obj = binding.objects[source_guid]
    if obj.type.name != 'Sprite':
        return result
    sprite = obj.read(check_read=False)
    original = copy.deepcopy(binding.recovery.fields(obj))
    rd = original['m_RD']
    if sprite.m_SpriteAtlas.path_id:
        atlas = sprite.m_SpriteAtlas.deref()
        packed, = [value for key, value in binding.recovery.fields(atlas)['m_RenderDataMap']
            if key == original['m_RenderDataKey']]
        rd.update(copy.deepcopy(packed))
    if rd['alphaTexture']['m_PathID'] or rd.get('secondaryTextures') or original.get('m_Bones'):
        raise ValueError('Restore additional original resource icon dependencies')
    _, _, docs = next(row for row in binding.queue if row[1] == result['guid'])
    native = docs[result['fileID']]['Sprite']
    texture = binding.load(native['m_RD']['texture']['guid'])
    # Converter rectangles describe the packed image. Unity's Sprite still
    # needs its original logical rectangle, pivot, mesh and effective Atlas UVs.
    rd['texture'] = dict(m_FileID=0, m_PathID=0)
    original['m_SpriteAtlas'] = dict(m_FileID=0, m_PathID=0)
    key, local = original['m_RenderDataKey']
    original['m_RenderDataKey'] = {''.join(format(key[f'data[{i}]'], '08x')[::-1] for i in range(4)): local}
    original['m_Rect']['serializedVersion'] = 2
    rd['serializedVersion'] = 3
    rd['textureRect']['serializedVersion'] = 2
    for submesh in rd['m_SubMeshes']:
        submesh['serializedVersion'] = 2
    rd['m_IndexBuffer'] = bytes(rd['m_IndexBuffer']).hex()
    vertices = rd['m_VertexData']
    raw = bytes(vertices['m_DataSize'])
    vertices.update(serializedVersion=3, m_DataSize=len(raw), _typelessdata=raw.hex())
    restored = source_header()
    restored.update(sprite_fields(original))
    restored['m_RD']['texture'] = texture
    docs[result['fileID']]['Sprite'] = restored
    mesh = MeshHandler(sprite.m_RD, obj.version)
    mesh.process()
    uv = rd['uvTransform']
    tex_obj = binding.original(next(key for key, row in binding.proof.items() if row.get('guid') == texture['guid']))
    tex = tex_obj.read(check_read=False)
    binding.proof[identity(obj)]['sprite'] = dict(rect={key: getattr(sprite.m_Rect, key) for key in ('x', 'y', 'width', 'height')},
        ppu=sprite.m_PixelsToUnits, pivot=dict(x=sprite.m_Pivot.x*sprite.m_Rect.width, y=sprite.m_Pivot.y*sprite.m_Rect.height),
        vertices=[dict(x=v[0], y=v[1]) for v in mesh.m_Vertices], triangles=list(mesh.m_IndexBuffer),
        uv=[dict(x=(v[0]*uv['x']+uv['y'])/tex.m_Width, y=(v[1]*uv['z']+uv['w'])/tex.m_Height) for v in mesh.m_Vertices])
    return result
