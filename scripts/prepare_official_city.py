"""Export source-backed Unity UI data from the selected official asset set."""
from __future__ import annotations

import hashlib
import io
import json
import math
from pathlib import Path
import struct

import numpy as np
from official_apk import Apk, ROOT
from UnityPy.classes.PPtr import PPtr
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
from official_unity_schema import ScriptSchemas


ART = ROOT / 'assets/official'
REFERENCE = ROOT / 'assets/reference/official-city'
UNITY_EDITOR_DATA = Path('D:/Tool/Unity/Editor/Data')


class PanelSchemas(ScriptSchemas):
    def __init__(self):
        self.generator = TypeTreeGenerator('2022.3.57f1', generator='AssetsTools')
        code = ROOT / '.tools/source-packages/official-code/managed/decoded'
        paths = {path.name: path for path in code.glob('*.dll')}
        paths.update({
            'UnityEngine.CoreModule.dll': UNITY_EDITOR_DATA / 'Managed/UnityEngine/UnityEngine.CoreModule.dll',
            'UnityEngine.DirectorModule.dll': UNITY_EDITOR_DATA / 'Managed/UnityEngine/UnityEngine.DirectorModule.dll',
            'UnityEngine.TextRenderingModule.dll': UNITY_EDITOR_DATA / 'Managed/UnityEngine/UnityEngine.TextRenderingModule.dll',
            'UnityEngine.UI.dll': ROOT / 'unity/Library/ScriptAssemblies/UnityEngine.UI.dll',
            'Unity.TextMeshPro.dll': ROOT / 'unity/Library/ScriptAssemblies/Unity.TextMeshPro.dll',
            'netstandard.dll': UNITY_EDITOR_DATA / 'NetStandard/ref/2.1.0/netstandard.dll',
            'mscorlib.dll': Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/mscorlib.dll'),
        })
        missing = [str(path) for path in paths.values() if not path.is_file()]
        if missing:
            raise FileNotFoundError('UI schema assemblies are missing: ' + ', '.join(missing))
        for path in paths.values():
            self.generator.load_dll(path.read_bytes())
        self.cache, self.records = {}, {}


def _vector(value, names):
    if isinstance(value, dict):
        return [float(value[name]) for name in names]
    return [float(getattr(value, name)) for name in names]


def transform(value):
    """Return Unity's local transform as a column-vector 4x4 matrix."""
    p = value.m_LocalPosition
    q = value.m_LocalRotation
    s = value.m_LocalScale
    x, y, z, w = (float(qv) for qv in (q.x, q.y, q.z, q.w))
    matrix = np.identity(4)
    matrix[:3, :3] = np.array([
        [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
        [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
        [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)],
    ]) @ np.diag([float(s.x), float(s.y), float(s.z)])
    matrix[:3, 3] = [float(p.x), float(p.y), float(p.z)]
    return matrix


class Exporter:
    def __init__(self, apk=None):
        self.apk = apk or Apk.current()
        if self.apk.version != '871_156598':
            raise ValueError('UI conversion requires the selected 871_156598 resource set')
        self.schemas = PanelSchemas()
        self.ui_schemas = self.schemas
        self.records = {}
        self.resources = self.records
        self.inputs = {}
        self.source_records = self.inputs
        self.unsupported = {}
        self.material_inputs = {}
        self.layout_inputs = {}
        self.language_sources = {}
        self._sprites = {}
        self._mono_names = {}
        self._language = None
        self._guid_paths = None
        self._guid_sprites = {}

    def input(self, obj):
        identity = obj.assets_file.name + ':' + str(obj.path_id)
        if identity not in self.inputs:
            self.inputs[identity] = dict(self.apk.provenance(obj),
                type=obj.type.name, object_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest())
        return self.inputs[identity]

    def mono(self, obj):
        basic = obj.read(check_read=False)
        script = basic.m_Script.deref().read()
        name = (script.m_Namespace + '.' if script.m_Namespace else '') + script.m_ClassName
        raw = obj.get_raw_data()
        self._mono_names[obj.assets_file.name + ':' + str(obj.path_id)] = name
        return name, raw, None

    def fields(self, obj):
        return self.schemas.read(obj)

    def text(self, key):
        import xxhash

        if self._language is None:
            self._load_language()
        digest = xxhash.xxh64_intdigest(key.encode('utf-8'))
        return self._language.get(digest, key)

    def _load_language(self):
        import xxhash

        package = self.apk.source.manifest('WwiseBnk', self.apk.archive)
        by_name = {row['name']: row for row in package['bundles']}
        names = (
            'wwisebnk_assets_launch_config_binary_language_zh_cn_text_g_bytes.rawfile',
            'wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile',
        )
        values = {}
        for name in names:
            try:
                record = by_name[name]
            except KeyError as ex:
                raise ValueError(f'871 Wwise manifest lacks required language input: {name}') from ex
            entry = f"assets/yoo/WwiseBnk/{record['file']}"
            if entry in self.apk.available:
                raw = self.apk.archive.read(entry)
                source = dict(origin='apk_zip', manifest=self.apk.version,
                    apk_manifest=self.apk.source.apk_version, apk_entry=entry,
                    raw_size=len(raw), raw_sha256=hashlib.sha256(raw).hexdigest())
            else:
                cached = self.apk.cloud.read('WwiseBnk', record)
                if cached is None:
                    raise FileNotFoundError(f'871 language bundle is absent from APK and verified cache: {name}')
                raw, source = cached
            if len(raw) != record['size'] or hashlib.md5(raw).hexdigest() != record['hash']:
                raise ValueError(f'871 Wwise language bundle checksum mismatch: {name}')
            count = struct.unpack_from('<i', raw)[0]
            start = 4 + count * 12
            if count < 0 or start > len(raw):
                raise ValueError(f'Invalid 871 Wwise language table header: {name}')
            for index in range(count):
                digest, offset = struct.unpack_from('<Qi', raw, 4 + index * 12)
                position = start + offset
                if position < start or position + 2 > len(raw):
                    raise ValueError(f'Invalid 871 Wwise language entry offset: {name}')
                length = struct.unpack_from('<H', raw, position)[0]
                if position + 2 + length > len(raw):
                    raise ValueError(f'Truncated 871 Wwise language entry: {name}')
                value = raw[position + 2:position + 2 + length].decode('utf-8', 'strict')
                values[digest] = value
            self.language_sources[name] = dict(source, bundle=name, bundle_hash=record['hash'],
                bytes=len(raw), sha256=hashlib.sha256(raw).hexdigest(), entries=count)
        self._language = values

    def guid_sprite(self, guid):
        if self._guid_paths is None:
            raw = self.apk.source.manifest_bytes('DefaultPackage', self.apk.archive)
            self._guid_paths = {match[2].decode(): match[1].decode()
                for match in __import__('re').finditer(rb'(Assets/[^\x00-\x1f]+?)\x20\x00([a-f0-9]{32})', raw)}
            self.guid_paths = self._guid_paths
        if guid in self._guid_sprites:
            return self._guid_sprites[guid]
        path = self._guid_paths.get(guid)
        if path is None:
            self.unsupported.setdefault('state_images_not_in_manifest', {})[guid] = 'GUID absent from selected 871 manifest'
            return None
        key = 'defaultpackage_' + path.replace('/', '_').lower()
        candidates = sorted((name for name in self.apk.index
            if key.startswith(name.removesuffix('.bundle'))), key=len, reverse=True)
        for bundle in candidates:
            try:
                match = next((ptr for asset, ptr in self.apk.container(bundle)
                    if asset.lower() == path.lower() and ptr.type.name == 'Sprite'), None)
            except FileNotFoundError:
                continue
            if match is not None:
                result = self.sprite(match)
                self._guid_sprites[guid] = result
                return result
        self.unsupported.setdefault('state_images_not_packaged', {})[guid] = path
        return None

    @staticmethod
    def text_mesh_pro_fields(component):
        raw = component.get_raw_data()
        if len(raw) < 92:
            raise ValueError('Original TMP component data is truncated')
        text_length = struct.unpack_from('<i', raw, 88)[0]
        if not 0 <= text_length <= len(raw) - 92:
            raise ValueError('Original TMP string extent is invalid')
        text = raw[92:92 + text_length].decode('utf-8', 'strict')
        aligned = (92 + text_length + 3) & ~3
        shared = struct.unpack_from('<i', raw, aligned + 28)[0]
        if not 0 <= shared <= 64:
            raise ValueError('Original TMP shared material count is invalid')
        material_offset = aligned + 44 + 12 * shared
        materials = struct.unpack_from('<i', raw, material_offset)[0]
        if not 0 <= materials <= 64:
            raise ValueError('Original TMP material count is invalid')
        style = aligned + 12 * (shared + materials)
        if style + 368 > len(raw):
            raise ValueError('Original TMP style extent is invalid')
        size = struct.unpack_from('<f', raw, style + 192)[0]
        color = struct.unpack_from('<4f', raw, style + 52)
        horizontal, vertical = struct.unpack_from('<2i', raw, style + 220)
        wrapping = struct.unpack_from('<i', raw, style + 256)[0]
        overflow = struct.unpack_from('<i', raw, style + 264)[0]
        margin = struct.unpack_from('<4f', raw, style + 352)
        if (not math.isfinite(size) or size <= 0 or horizontal not in range(9)
                or vertical not in (0, 256, 512, 1024, 2048, 4096, 8192)
                or wrapping not in (0, 1) or overflow not in range(7)
                or not all(math.isfinite(value) for value in color + margin)):
            raise ValueError('Original TMP style values are invalid')
        return {'m_text': text, 'm_fontSize': size,
                'm_fontColor': dict(zip('rgba', color)),
                'm_HorizontalAlignment': horizontal, 'm_VerticalAlignment': vertical,
                'm_enableWordWrapping': wrapping, 'm_overflowMode': overflow,
                'm_margin': dict(zip('xyzw', margin))}

    def object_path(self, ptr):
        obj = ptr.deref() if hasattr(ptr, 'deref') else ptr
        if obj.type.name != 'GameObject':
            obj = obj.read(check_read=False).m_GameObject.deref()
        names = [obj.read().m_Name]
        transform_obj = self._transform_component(obj)
        transform = transform_obj.read()
        while transform.m_Father.path_id:
            transform = transform.m_Father.deref().read()
            names.insert(0, transform.m_GameObject.deref().read().m_Name)
        return '/' + '/'.join(names)

    @staticmethod
    def _transform_component(obj):
        return next(part.component.deref() for part in obj.read().m_Component
                    if part.component.type.name in ('RectTransform', 'Transform'))

    def target(self, owner, value):
        ptr = PPtr(**value, assetsfile=owner.assets_file)
        if not ptr.path_id:
            return None
        target = ptr.deref()
        if target.type.name == 'Sprite':
            return self.sprite(ptr)
        if target.type.name in ('GameObject', 'Transform', 'RectTransform'):
            return self.object_path(ptr)
        data = target.read(check_read=False)
        if hasattr(data, 'm_GameObject') and data.m_GameObject.path_id:
            return self.object_path(data.m_GameObject)
        return dict(self.apk.provenance(target), type=target.type.name)

    def sprite(self, ptr):
        obj = ptr.deref() if hasattr(ptr, 'deref') else ptr
        key = obj.assets_file.name + ':' + str(obj.path_id)
        if key in self._sprites:
            return self._sprites[key]
        source = obj.read()
        stream = io.BytesIO()
        source.image.save(stream, format='PNG', optimize=False)
        raw = stream.getvalue()
        digest = hashlib.sha256(raw).hexdigest()
        filename = f'{source.m_Name}-{digest[:10]}.png'
        target = ART / 'sprites' / filename
        target.parent.mkdir(parents=True, exist_ok=True)
        if target.exists() and hashlib.sha256(target.read_bytes()).hexdigest() != digest:
            raise ValueError('Sprite output path collision: ' + filename)
        if not target.exists():
            target.write_bytes(raw)
        uri = 'res://assets/official/sprites/' + filename
        result = dict(file=uri, name=source.m_Name,
            width=int(source.m_Rect.width), height=int(source.m_Rect.height),
            border=_vector(source.m_Border, 'xyzw'), ppu=float(source.m_PixelsToUnits),
            pivot=[float(source.m_Pivot.x * source.m_Rect.width),
                   float(source.m_Pivot.y * source.m_Rect.height)])
        self.records[uri] = dict(bytes=len(raw), sha256=digest, source=self.input(obj))
        self._sprites[key] = result
        return result

    def _component_data(self, component):
        kind = component.type.name
        if kind == 'MonoBehaviour':
            name, raw, _ = self.mono(component)
            self.input(component)
            short = name.rsplit('.', 1)[-1]
            fields = (self.text_mesh_pro_fields(component) if short == 'TextMeshProUGUI' else
                self.schemas.read(component) if short in {
                'Image', 'RawImage', 'Text', 'TextMeshPro',
                'Button', 'CanvasGroup', 'Mask', 'RectMask2D', 'LayoutElement',
                'ContentSizeFitter', 'HorizontalLayoutGroup', 'VerticalLayoutGroup',
                'GridLayoutGroup'
            } else {})
            return short, fields, raw
        try:
            return kind, component.read_typetree(), component.get_raw_data()
        except Exception:
            return kind, {}, component.get_raw_data()

    def ui_node(self, obj, path=''):
        if obj.type.name != 'GameObject':
            return None
        game_object = obj.read()
        transform_obj = self._transform_component(obj)
        layout = transform_obj.read()
        name = game_object.m_Name
        path = path + '/' + name
        q = layout.m_LocalRotation
        rotation = math.degrees(2 * math.atan2(float(q.z), float(q.w)))
        node = dict(name=name, path=path, active=bool(game_object.m_IsActive),
            anchor_min=_vector(getattr(layout, 'm_AnchorMin', None), 'xy') if hasattr(layout, 'm_AnchorMin') else [0.0, 0.0],
            anchor_max=_vector(getattr(layout, 'm_AnchorMax', None), 'xy') if hasattr(layout, 'm_AnchorMax') else [0.0, 0.0],
            anchored_position=_vector(getattr(layout, 'm_AnchoredPosition', None), 'xy') if hasattr(layout, 'm_AnchoredPosition') else _vector(layout.m_LocalPosition, 'xy'),
            size_delta=_vector(getattr(layout, 'm_SizeDelta', None), 'xy') if hasattr(layout, 'm_SizeDelta') else [0.0, 0.0],
            pivot=_vector(getattr(layout, 'm_Pivot', None), 'xy') if hasattr(layout, 'm_Pivot') else [0.5, 0.5],
            scale=_vector(layout.m_LocalScale, 'xyz'), rotation=rotation,
            scripts=[], children=[])
        for child in layout.m_Children:
            child_node = self.ui_node(child.read().m_GameObject.deref(), path)
            if child_node is not None:
                node['children'].append(child_node)

        for entry in game_object.m_Component:
            component = entry.component.deref()
            kind, fields, raw = self._component_data(component)
            short = kind.rsplit('.', 1)[-1]
            if component.type.name == 'Transform' or component.type.name == 'RectTransform':
                continue
            if component.type.name == 'MonoBehaviour':
                node['scripts'].append(short)
            if short in ('Image', 'RawImage'):
                sprite_value = fields.get('m_Sprite')
                sprite = self.target(component, sprite_value) if sprite_value and sprite_value.get('m_PathID') else None
                node['image'] = dict(sprite=sprite, color=_vector(fields.get('m_Color'), 'rgba'),
                    type=int(fields.get('m_Type', 0)), preserve_aspect=bool(fields.get('m_PreserveAspect', False)),
                    graphic_flags=True, raycast_target=bool(fields.get('m_RaycastTarget', True)),
                    maskable=bool(fields.get('m_Maskable', True)))
            elif short in ('Text', 'TextMeshProUGUI', 'TextMeshPro'):
                color = fields.get('m_Color', fields.get('m_fontColor'))
                alignment = int(fields.get('m_Alignment', 0))
                horizontal = int(fields.get('m_HorizontalAlignment', alignment % 3))
                vertical = int(fields.get('m_VerticalAlignment', (alignment // 3) * 512))
                node['text'] = dict(value=fields.get('m_Text', fields.get('m_text', '')),
                    color=_vector(color, 'rgba') if color is not None else [1.0, 1.0, 1.0, 1.0],
                    font_size=float(fields.get('m_FontSize', fields.get('m_fontSize', 14.0))),
                    horizontal=horizontal, vertical=vertical,
                    word_wrap=bool(fields.get('m_enableWordWrapping', True)),
                    overflow=int(fields.get('m_overflowMode', 0)),
                    margin=_vector(fields.get('m_margin'), 'xyzw') if fields.get('m_margin') else [0.0] * 4)
            elif short == 'Button':
                node['button'] = True
            elif short == 'CanvasGroup':
                node['alpha'] = float(fields.get('m_Alpha', 1.0))
            elif short == 'Mask':
                node['mask'] = bool(fields.get('m_ShowMaskGraphic', True))
            elif short == 'RectMask2D':
                node['rectmask2d'] = True
            elif short in ('LoopVerticalScrollRect', 'LoopHorizontalScrollRect'):
                node['scroll'] = dict(horizontal=short == 'LoopHorizontalScrollRect',
                    vertical=short == 'LoopVerticalScrollRect', sensitivity=90)
            elif short == 'NoDrawingRayCast':
                node['no_drawing_raycast'] = True
                node['raycast_target'] = True
                node['raycast_padding'] = [0.0, 0.0, 0.0, 0.0]
            elif short in ('VerticalLayoutGroup', 'HorizontalLayoutGroup'):
                padding = fields.get('m_Padding', {})
                node['layout'] = dict(axis=1 if short == 'VerticalLayoutGroup' else 0,
                    padding=[padding.get(k, 0) for k in ('m_Left', 'm_Right', 'm_Top', 'm_Bottom')],
                    alignment=int(fields.get('m_ChildAlignment', 0)),
                    spacing=float(fields.get('m_Spacing', 0.0)),
                    expand=[bool(fields.get('m_ChildForceExpandWidth', False)), bool(fields.get('m_ChildForceExpandHeight', False))],
                    control=[bool(fields.get('m_ChildControlWidth', True)), bool(fields.get('m_ChildControlHeight', True))],
                    child_scale=[bool(fields.get('m_ChildScaleWidth', False)), bool(fields.get('m_ChildScaleHeight', False))],
                    reverse=bool(fields.get('m_ReverseArrangement', False)))
            elif short == 'GridLayoutGroup':
                padding = fields.get('m_Padding', {})
                node['grid'] = dict(padding=[padding.get(k, 0) for k in ('m_Left', 'm_Right', 'm_Top', 'm_Bottom')],
                    alignment=int(fields.get('m_ChildAlignment', 0)), corner=int(fields.get('m_StartCorner', 0)),
                    axis=int(fields.get('m_StartAxis', 0)), cell=_vector(fields.get('m_CellSize'), 'xy'),
                    spacing=_vector(fields.get('m_Spacing'), 'xy'), constraint=int(fields.get('m_Constraint', 0)),
                    count=int(fields.get('m_ConstraintCount', 1)), vertical_spacing=[], horizontal_spacing=[])
            elif short == 'ContentSizeFitter':
                node['size_fitter'] = [int(fields.get('m_HorizontalFit', 0)), int(fields.get('m_VerticalFit', 0))]
            elif short == 'LayoutElement':
                node['layout_element'] = dict(ignore=bool(fields.get('m_IgnoreLayout', False)),
                    min=[float(fields.get('m_MinWidth', -1)), float(fields.get('m_MinHeight', -1))],
                    preferred=[float(fields.get('m_PreferredWidth', -1)), float(fields.get('m_PreferredHeight', -1))],
                    flexible=[float(fields.get('m_FlexibleWidth', -1)), float(fields.get('m_FlexibleHeight', -1))])
        return node

    def ui(self, bundle, suffix):
        obj = self.apk.asset(bundle, suffix)
        if obj.type.name != 'GameObject':
            raise ValueError(f'UI prefab root is not a GameObject: {bundle}{suffix} ({obj.type.name})')
        source = dict(self.apk.provenance(obj), prefab=suffix)
        self.input(obj)
        root = self.ui_node(obj)
        return dict(source=source, root=root)

    def save(self, name, value):
        target = ART / name
        target.parent.mkdir(parents=True, exist_ok=True)
        data = (json.dumps(value, ensure_ascii=False, separators=(',', ':'), allow_nan=False) + '\n').encode('utf-8')
        temporary = target.with_suffix(target.suffix + '.tmp')
        temporary.write_bytes(data)
        temporary.replace(target)
        return target
