"""Resolve original Sprite atlas data into native Sprite/full-mip Texture2D assets."""
import copy
import hashlib
import struct
import yaml
from UnityPy.enums import TextureFormat
from UnityPy.helpers.MeshHelper import MeshHandler
from prepare_unity_profession_reward_sprites import Dumper, fields, source_header
from prepare_unity_map13_native_scene import decode_mips
from official_apk import ROOT


def sha(data):
    return hashlib.sha256(data).hexdigest()


class NativeTeamSprites:
    def __init__(self, apk):
        self.apk = apk
        self.outputs = {}
        self.textures = {}
        self.sprites = {}
        self.folder = ROOT / 'unity/Assets/Official/World/Map/12/UI/Team/FullSprites'
        self.outputs[self.folder.with_name(self.folder.name + '.meta')] = (
            'fileFormatVersion: 2\nguid: ' + sha(b'zjcs:map12:team-full-sprites-folder')[:32] +
            '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n').encode()

    def write(self, kind, obj, native):
        identity = obj.assets_file.name + ':' + str(obj.path_id)
        name = obj.read().m_Name + '-' + sha(identity.encode())[:10]
        path = self.folder / (name + ('.sprite.asset' if kind == 'Sprite' else '.texture2d.asset'))
        guid = hashlib.md5(('zjcs:map12:team-full:' + identity).encode()).hexdigest()
        main_id, class_id = (21300000, 213) if kind == 'Sprite' else (2800000, 28)
        body = yaml.dump({kind: native}, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
        raw = ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!' + str(class_id) +
               ' &' + str(main_id) + '\n' + body).encode()
        self.outputs[path] = raw
        self.outputs[path.with_name(path.name + '.meta')] = ('fileFormatVersion: 2\nguid: ' + guid +
            '\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: ' + str(main_id) + '\n').encode()
        return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(raw), guid=guid,
                    identity=identity, source=self.apk.provenance(obj), object_sha256=sha(obj.get_raw_data()))

    def texture(self, obj):
        identity = obj.assets_file.name + ':' + str(obj.path_id)
        if identity in self.textures:
            return self.textures[identity]
        texture = obj.read()
        if texture.m_MipsStripped or texture.m_ImageCount != 1 or texture.m_TextureDimension != 2:
            raise ValueError('Inspect original team texture topology')
        encoded = texture.get_image_data()
        if 'ASTC' in TextureFormat(texture.m_TextureFormat).name:
            pixels, mips, profile = decode_mips(texture)
            target_format = 4
        else:
            target_format = int(texture.m_TextureFormat)
            if target_format not in (1, 4):
                raise ValueError('Resolve original team texture format')
            pixels, profile, mips = encoded, 'original-uncompressed', []
            offset = 0
            for level in range(texture.m_MipCount):
                width, height = max(1, texture.m_Width >> level), max(1, texture.m_Height >> level)
                size = width * height * (1 if target_format == 1 else 4)
                mips.append(dict(level=level, width=width, height=height, rgba_offset=offset,
                                 rgba_size=size, rgba_sha256=sha(pixels[offset:offset + size])))
                offset += size
            if offset != len(pixels):
                raise ValueError('Original team mip payload not fully consumed')
        original = obj.read_typetree()
        original.pop('image data')
        native = source_header()
        native.update(fields(copy.deepcopy(original)))
        native.update(serializedVersion=2, m_TextureFormat=target_format, m_CompleteImageSize=len(pixels),
                      m_PlatformBlob='', m_StreamData=dict(serializedVersion=2, offset=0, size=0, path=''))
        native['m_TextureSettings']['serializedVersion'] = 2
        native['image data'], native['_typelessdata'] = len(pixels), pixels.hex()
        output = self.write('Texture2D', obj, native)
        output.update(original_fields=original, encoded_sha256=sha(encoded), pixels_sha256=sha(pixels),
                      mips=mips, profile=profile, format=target_format)
        self.textures[identity] = output
        return output

    def sprite(self, obj):
        identity = obj.assets_file.name + ':' + str(obj.path_id)
        if identity in self.sprites:
            return self.sprites[identity]
        sprite = obj.read()
        original = copy.deepcopy(obj.read_typetree())
        rd = original['m_RD']
        atlas_source = None
        runtime_rd = sprite.m_RD
        if sprite.m_SpriteAtlas.path_id:
            atlas = sprite.m_SpriteAtlas.deref()
            original_atlas = atlas.read_typetree()
            atlas_rd, = [value for key, value in original_atlas['m_RenderDataMap']
                         if key == original['m_RenderDataKey']]
            rd.update(copy.deepcopy(atlas_rd))
            runtime_rd = dict(sprite.m_SpriteAtlas.read().m_RenderDataMap)[sprite.m_RenderDataKey]
            atlas_source = dict(source=self.apk.provenance(atlas), object_sha256=sha(atlas.get_raw_data()),
                                render_data=copy.deepcopy(atlas_rd))
        if (runtime_rd.alphaTexture.path_id or runtime_rd.secondaryTextures or sprite.m_Bones
                or sprite.m_PhysicsShape or sprite.m_RD.m_Bindpose):
            raise ValueError('Resolve additional original team Sprite dependencies')
        if (runtime_rd.settingsRaw >> 2) & 15:
            raise ValueError('Resolve original team Sprite packed rotation before baking atlas UV')
        texture_obj = runtime_rd.texture.deref()
        texture = texture_obj.read()
        texture_output = self.texture(texture_obj)
        original['m_SpriteAtlas'] = dict(m_FileID=0, m_PathID=0)
        rd['texture'] = dict(m_FileID=0, m_PathID=0)
        key, local_id = original['m_RenderDataKey']
        original['m_RenderDataKey'] = {''.join(f"{key['data[' + str(i) + ']']:08x}"[::-1]
                                               for i in range(4)): local_id}
        original['m_Rect']['serializedVersion'] = 2
        rd['serializedVersion'] = 3
        rd['textureRect']['serializedVersion'] = 2
        for submesh in rd['m_SubMeshes']:
            submesh['serializedVersion'] = 2
        rd['m_IndexBuffer'] = bytes(rd['m_IndexBuffer']).hex()
        mesh = MeshHandler(sprite.m_RD, obj.version)
        mesh.process()
        vertices = mesh.m_Vertices
        uv_transform = runtime_rd.uvTransform
        uvs = [(vertex[0] * uv_transform.x + uv_transform.y,
                vertex[1] * uv_transform.z + uv_transform.w) for vertex in vertices]
        uvs = [(u / texture.m_Width, v / texture.m_Height) for u, v in uvs]
        vd = rd['m_VertexData']
        active = [(c['stream'], c['offset'], c['format'], c['dimension'])
                  for c in vd['m_Channels'] if c['dimension']]
        if active != [(0, 0, 0, 3), (1, 0, 0, 2)]:
            raise ValueError('Inspect original team Sprite position/UV stream layout')
        vertex_bytes = bytearray(vd['m_DataSize'])
        uv_offset = (len(vertices) * 12 + 15) // 16 * 16
        if len(vertex_bytes) != uv_offset + len(vertices) * 8:
            raise ValueError('Original team Sprite native stream alignment differs')
        # Resolve the same atlas transform the original runtime applies to this
        # mesh. Keep source position/index bytes and every logical Sprite field.
        vertex_bytes[uv_offset:] = b''.join(struct.pack('<ff', u, v) for u, v in uvs)
        vd.update(serializedVersion=3, m_DataSize=len(vertex_bytes), _typelessdata=bytes(vertex_bytes).hex())
        native = source_header()
        native.update(fields(original))
        native['m_RD']['texture'] = dict(fileID=2800000, guid=texture_output['guid'], type=2)
        output = self.write('Sprite', obj, native)
        output.update(texture=texture_output['identity'], atlas=atlas_source,
            expected=dict(name=sprite.m_Name, path=output['path'].removeprefix('unity/'),
                guid=output['guid'], rect={key: getattr(sprite.m_Rect, key) for key in ('x', 'y', 'width', 'height')},
                pivot=dict(x=sprite.m_Pivot.x * sprite.m_Rect.width, y=sprite.m_Pivot.y * sprite.m_Rect.height),
                ppu=sprite.m_PixelsToUnits, border={key: getattr(sprite.m_Border, key) for key in 'xyzw'},
                vertices=[dict(x=vertex[0], y=vertex[1]) for vertex in vertices], triangles=list(mesh.m_IndexBuffer),
                uv=[dict(x=u, y=v) for u, v in uvs]))
        self.sprites[identity] = output
        return output
