"""Extract the pinned main-scene Day_Common timeline and its official sprites."""
from __future__ import annotations

import hashlib
import io
import json
import math
from pathlib import Path
import sys


ROOT = Path(__file__).resolve().parents[1]
VERSION = '854_156383'
MAIN_BUNDLE = 'defaultpackage_assets_scenes_main.bundle'
OUTPUT = ROOT / 'assets/official/home-environment/sky-timeline.json'
SPRITE_OUTPUT = ROOT / 'assets/official/home-environment/day-sky-sprites'
MANIFEST_OUTPUT = ROOT / 'assets/reference/home-environment/solar-timeline-source-manifest.json'

sys.path.insert(0, str(ROOT / '.tools/character-art-python'))
sys.path.insert(0, str(ROOT / 'scripts'))

from UnityPy.classes.PPtr import PPtr
from UnityPy.export.SpriteHelper import get_image_from_sprite
from official_animation import mecanim_tracks
from official_apk import Apk
from official_unity_schema import ScriptSchemas


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def pointer(value: dict, owner):
    return PPtr(**value, assetsfile=owner.assets_file).deref() if value and value.get('m_PathID') else None


def components(game_object):
    return [entry.component.deref() for entry in game_object.read().m_Component]


def transform_component(game_object):
    return next(component for component in components(game_object)
                if component.type.name in ('Transform', 'RectTransform'))


def scene_tree(game_object, parent_path=''):
    name = game_object.read().m_Name
    path = parent_path + '/' + name
    return {
        'name': name,
        'path': path,
        'children': [scene_tree(child.read().m_GameObject.deref(), path)
                     for child in transform_component(game_object).read().m_Children],
    }


def pad_sprite_image(image, logical_size, texture_rect_offset):
    from PIL import Image

    image = image.convert('RGBA')
    width, height = logical_size
    x = int(round(float(texture_rect_offset['x'])))
    y_bottom = int(round(float(texture_rect_offset['y'])))
    if width <= 0 or height <= 0 or x < 0 or y_bottom < 0:
        raise ValueError('Official home sky sprite has invalid dimensions or trim offset.')
    if x + image.width > width or y_bottom + image.height > height:
        raise ValueError('Official home sky sprite trim exceeds its source rectangle.')
    canvas = Image.new('RGBA', (width, height), (0, 0, 0, 0))
    canvas.paste(image, (x, height - y_bottom - image.height))
    return canvas


def serializable_track(track):
    stepped = track['property'] in ('m_IsActive', 'm_Enabled', 'm_BlocksRaycasts')
    keys = []
    for row in track['keys']:
        if len(row) < 2:
            raise ValueError('Invalid official home sky animation key.')
        keys.append({
            'time': float(row[0]),
            'value': float(row[1]),
            'inTangent': 0.0 if len(row) < 3 or row[2] is None else float(row[2]),
            'outTangent': 0.0 if len(row) < 4 or row[3] is None else float(row[3]),
        })
    return {'path': track['path'], 'property': track['property'], 'stepped': stepped, 'keys': keys}


def validate_track_targets(tracks, nodes, renderers, lights):
    node_paths = {node['path'] for node in nodes}
    renderer_paths = {renderer['path'] for renderer in renderers}
    light_paths = {light['path'] for light in lights}
    seen = set()
    for track in tracks:
        path, prop = track['path'], track['property']
        key = (path, prop)
        if key in seen:
            raise ValueError(f'Duplicate official Home sky animation binding: {path} {prop}')
        seen.add(key)
        if path not in node_paths:
            raise ValueError('Official Home sky animation target is missing: ' + path)
        if prop in ('position.x', 'position.y', 'position.z', 'scale.x', 'scale.y', 'scale.z', 'm_IsActive'):
            continue
        if prop == 'm_Enabled' and (path in renderer_paths or path in light_paths):
            continue
        if prop in ('m_Color.r', 'm_Color.g', 'm_Color.b', 'm_Color.a') and (path in renderer_paths or path in light_paths):
            continue
        if prop in ('m_Intensity', 'm_ShadowIntensity') and path in light_paths:
            continue
        raise ValueError(f'Unsupported official Home sky animation target: {path} {prop}')


def component_class(component):
    if component.type.name != 'MonoBehaviour':
        return component.type.name
    script = component.read(check_read=False).m_Script.read()
    return script.m_ClassName


def extract():
    apk = Apk(VERSION)
    schemas = ScriptSchemas()
    inputs = {}

    def record_input(obj):
        provenance = apk.provenance(obj)
        raw = obj.get_raw_data()
        key = provenance['bundle'] + ':' + str(obj.path_id)
        name = ''
        try:
            name = obj.read(check_read=False).m_Name
        except Exception:
            pass
        value = dict(provenance, type=obj.type.name, name=name,
                     bytes=len(raw), sha256=sha256_bytes(raw))
        previous = inputs.get(key)
        if previous is not None and previous != value:
            raise ValueError('Conflicting official Home sky source object: ' + key)
        inputs[key] = value
        return value

    managers = []
    for obj in apk.objects(MAIN_BUNDLE):
        if obj.type.name != 'MonoBehaviour':
            continue
        if component_class(obj) == 'HomeEnvironmentManagerMono':
            managers.append(obj)
    if len(managers) != 1:
        raise ValueError('Expected one HomeEnvironmentManagerMono in the pinned main scene.')
    manager = managers[0]
    manager_data = schemas.read(manager)
    record_input(manager)

    playable = pointer(manager_data['HomeTimeDriver']['Playable'], manager)
    record_input(playable)
    playable_data = schemas.read(playable)
    timeline = pointer(playable_data['DefaultTimelineAsset'], playable)
    record_input(timeline)
    timeline_data = schemas.read(timeline)
    binding = pointer(playable_data['PlayableBinding'], playable)
    record_input(binding)
    day_binding = next((row for row in schemas.read(binding)['AnimatorBindings'] if row['Key'] == 'Day'), None)
    if day_binding is None:
        raise ValueError('HomeTimeline has no official Day Animator binding.')
    animator = pointer(day_binding['Animator'], binding)
    record_input(animator)
    day_root = animator.read().m_GameObject.deref()
    day_tree = scene_tree(day_root)

    day_tracks = []

    def collect_tracks(track_ptr, owner):
        track = pointer(track_ptr, owner)
        if track is None:
            return
        record_input(track)
        track_data = schemas.read(track)
        if track_data.get('m_Name') == 'Day':
            day_tracks.append((track, track_data))
        for child in track_data.get('m_Children', []):
            collect_tracks(child, track)

    for track_ptr in timeline_data.get('m_Tracks', []):
        collect_tracks(track_ptr, timeline)
    if len(day_tracks) != 1:
        raise ValueError('HomeTimeline must contain exactly one Day track.')
    day_track, day_track_data = day_tracks[0]
    if len(day_track_data.get('m_Clips', [])) != 1:
        raise ValueError('HomeTimeline Day track must contain exactly one clip.')
    clip_record = day_track_data['m_Clips'][0]
    playable_clip = pointer(clip_record['m_Asset'], day_track)
    record_input(playable_clip)
    playable_clip_data = schemas.read(playable_clip)
    animation_clip = pointer(playable_clip_data['m_Clip'], playable_clip)
    record_input(animation_clip)

    tracks, clip_duration, clip_loop, unknown = mecanim_tracks(
        animation_clip, day_tree, property_names=['m_ShadowIntensity'])
    if unknown or not tracks:
        raise ValueError('Official Day_Common animation contains unresolved bindings: '
                         + json.dumps(unknown, ensure_ascii=False))
    all_paths = set()

    def add_paths(node):
        all_paths.add(node['path'])
        for child in node['children']:
            add_paths(child)

    add_paths(day_tree)
    for track in tracks:
        if track['path'] not in all_paths:
            raise ValueError('Official Day_Common track references a missing node: ' + track['path'])

    nodes = []
    renderers = []
    lights = []
    sprite_files = {}
    materials = {}
    renderer_count = 0

    def add_node(game_object, parent_path=''):
        nonlocal renderer_count
        go_data = game_object.read()
        name = go_data.m_Name
        path = parent_path + '/' + name
        transform = transform_component(game_object).read()
        position = transform.m_LocalPosition
        rotation = transform.m_LocalRotation
        scale = transform.m_LocalScale
        node = {
            'path': path,
            'parent': parent_path,
            'name': name,
            'position': [position.x, position.y, position.z],
            'rotation': [rotation.x, rotation.y, rotation.z, rotation.w],
            'scale': [scale.x, scale.y, scale.z],
            'active': bool(go_data.m_IsActive),
        }
        nodes.append(node)
        record_input(game_object)
        for component in components(game_object):
            kind = component_class(component)
            record_input(component)
            if kind == 'CameraFollower':
                data = schemas.read(component)
                node['cameraFollower'] = {
                    'centerX': float(data['myCameraCenterX']),
                    'threshold': float(data['myThreshold']),
                }
            elif kind == 'Light2D':
                lights.append({'path': path, 'enabled': bool(component.read(check_read=False).m_Enabled)})
            elif kind == 'SpriteRenderer':
                renderer = component.read()
                sprite = renderer.m_Sprite.deref()
                sprite_data = sprite.read()
                sprite_name = sprite_data.m_Name
                record_input(sprite)
                image = get_image_from_sprite(sprite_data)
                rect = sprite_data.m_Rect
                offset = sprite_data.m_RD.textureRectOffset
                logical_size = (int(round(rect.width)), int(round(rect.height)))
                image = pad_sprite_image(image, logical_size, {'x': offset.x, 'y': offset.y})
                png = io.BytesIO()
                image.save(png, format='PNG', optimize=False)
                png_bytes = png.getvalue()
                filename = f'{sprite_name}-{sha256_bytes(png_bytes)[:10]}.png'
                sprite_files[filename] = png_bytes

                material_ptr = renderer.m_Materials[0] if renderer.m_Materials else None
                material = pointer({'m_FileID': material_ptr.m_FileID, 'm_PathID': material_ptr.m_PathID}, component) if material_ptr else None
                if material is None:
                    raise ValueError('Official Day_Common SpriteRenderer has no material: ' + path)
                record_input(material)
                material_data = material.read()
                shader = material_data.m_Shader.deref()
                record_input(shader)
                shader_name = shader.read().m_ParsedForm.m_Name
                saved = material_data.m_SavedProperties
                material_record = {
                    'name': material_data.m_Name,
                    'shader': shader_name,
                    'floats': [{'name': key, 'value': float(value)} for key, value in saved.m_Floats],
                    'colors': [{'name': key, 'value': [value.r, value.g, value.b, value.a]}
                               for key, value in saved.m_Colors],
                }
                materials[material_record['name']] = material_record
                color = renderer.m_Color
                renderers.append({
                    'path': path,
                    'sprite': sprite_name,
                    'file': filename,
                    'rect': [float(rect.width), float(rect.height)],
                    'pivot': [float(sprite_data.m_Pivot.x), float(sprite_data.m_Pivot.y)],
                    'pixelsPerUnit': float(sprite_data.m_PixelsToUnits),
                    'extrude': int(sprite_data.m_Extrude),
                    'meshType': 'Tight' if bool(sprite_data.m_IsPolygon) else 'FullRect',
                    'enabled': bool(component.read(check_read=False).m_Enabled),
                    'flipX': bool(getattr(renderer, 'm_FlipX', False)),
                    'flipY': bool(getattr(renderer, 'm_FlipY', False)),
                    'color': [color.r, color.g, color.b, color.a],
                    'sortingLayer': int(getattr(renderer, 'm_SortingLayerID', getattr(renderer, 'm_SortingLayer', 0))),
                    'sortingOrder': int(renderer.m_SortingOrder),
                    'material': material_record['name'],
                })
                renderer_count += 1
        for child_ptr in transform_component(game_object).read().m_Children:
            add_node(child_ptr.read().m_GameObject.deref(), path)

    add_node(day_root)
    if renderer_count == 0 or len(sprite_files) == 0:
        raise ValueError('Official Day_Common hierarchy contains no SpriteRenderer assets.')
    validate_track_targets(tracks, nodes, renderers, lights)

    source_code = ROOT / '.tools/source-packages/official-code/client-source'
    timeline_driver = source_code / 'HomeTimeDriver.cs'
    camera_follower = source_code / 'CameraFollower.cs'
    runtime_code = ROOT / 'unity/Assets/Game/Runtime/Presentation/Home'
    timeline_duration = max(float(clip_record['m_Start']) + float(clip_record['m_Duration']), clip_duration)
    runtime = {
        'sourceManifest': VERSION,
        'sourceTimeline': timeline_data['m_Name'],
        'sourceClip': animation_clip.read().m_Name,
        'duration': float(timeline_duration),
        'clipStart': float(clip_record['m_Start']),
        'clipDuration': float(clip_record['m_Duration']),
        'clipIn': float(clip_record['m_ClipIn']),
        'clipSpeed': float(clip_record['m_TimeScale']),
        'clipLoop': bool(playable_clip_data['m_Loop']),
        'loop': bool(clip_loop),
        'rootPath': day_tree['path'],
        'nodes': nodes,
        'renderers': renderers,
        'lights': lights,
        'materials': list(materials.values()),
        'tracks': [serializable_track(track) for track in tracks],
    }
    output_bytes = (json.dumps(runtime, ensure_ascii=False, separators=(',', ':'), allow_nan=False) + '\n').encode('utf-8')
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    SPRITE_OUTPUT.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_bytes(output_bytes)
    outputs = {OUTPUT.relative_to(ROOT).as_posix(): sha256_bytes(output_bytes)}
    for filename, data in sorted(sprite_files.items()):
        target = SPRITE_OUTPUT / filename
        target.write_bytes(data)
        outputs[target.relative_to(ROOT).as_posix()] = sha256_bytes(data)
    source_script = Path(__file__).resolve()
    manifest = {
        'manifest': VERSION,
        'timeline': {
            'name': timeline_data['m_Name'],
            'path_id': str(timeline.path_id),
            'duration': runtime['duration'],
            'day_track_path_id': str(day_track.path_id),
            'clip_path_id': str(animation_clip.path_id),
            'clip_name': runtime['sourceClip'],
            'curve_count': len(tracks),
            'renderer_count': renderer_count,
            'light_count': len(lights),
        },
        'inputs': dict(sorted(inputs.items())),
        'code': {
            'extractor_sha256': sha256_bytes(source_script.read_bytes()),
            'animation_decoder_sha256': sha256_bytes((ROOT / 'scripts/official_animation.py').read_bytes()),
            'home_time_driver_sha256': sha256_bytes(timeline_driver.read_bytes()),
            'camera_follower_sha256': sha256_bytes(camera_follower.read_bytes()),
            'HomeEnvironmentView.cs': sha256_bytes((runtime_code / 'HomeEnvironmentView.cs').read_bytes()),
            'HomeFeatureRuntime.cs': sha256_bytes((runtime_code / 'HomeFeatureRuntime.cs').read_bytes()),
            'HomeSceneView.cs': sha256_bytes((runtime_code / 'HomeSceneView.cs').read_bytes()),
            'HomeSkyTimelineView.cs': sha256_bytes((runtime_code / 'HomeSkyTimelineView.cs').read_bytes()),
            'HomeSkyCameraFollower.cs': sha256_bytes((runtime_code / 'HomeSkyCameraFollower.cs').read_bytes()),
        },
        'outputs': dict(sorted(outputs.items())),
        'operation': 'Decode the pinned HomeTimeline Day track with official Mecanim bindings; export actual Sprite objects, restore atlas trim offsets into original logical rectangles, and retain original GameObject transforms, SpriteRenderer colors/materials, Light2D targets, and camera follower parameters.',
    }
    MANIFEST_OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST_OUTPUT.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(f'Extracted {runtime["sourceClip"]}: {len(tracks)} tracks, {renderer_count} sprites, '
          f'{len(lights)} lights, duration={runtime["duration"]:g}s.')
    print('Wrote', OUTPUT.relative_to(ROOT))
    print('Wrote source record', MANIFEST_OUTPUT.relative_to(ROOT))


if __name__ == '__main__':
    extract()
