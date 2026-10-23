"""Refresh changed decoded official sprites and their existing metadata bindings."""
from __future__ import annotations

import argparse
import hashlib
import io
import json
from pathlib import Path
import subprocess

from activate_current_payloads import activate
from current_conversion_state import activated_receipt
from official_apk import Apk, ROOT
from UnityPy.helpers.MeshHelper import MeshHandler


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def metadata(obj):
    sprite = obj.read()
    image = sprite.image
    mesh = MeshHandler(sprite.m_RD, obj.version)
    mesh.process()
    points = mesh.m_Vertices
    x0, y0 = min(p[0] for p in points), min(p[1] for p in points)
    x1, y1 = max(p[0] for p in points), max(p[1] for p in points)
    ppu = sprite.m_PixelsToUnits
    result = dict(width=image.width, height=image.height,
                  border=[sprite.m_Border.x, sprite.m_Border.y, sprite.m_Border.z, sprite.m_Border.w],
                  ppu=ppu, rect_size=[sprite.m_Rect.width, sprite.m_Rect.height],
                  trim_offset=[x0*ppu + sprite.m_Rect.width*sprite.m_Pivot.x,
                               y0*ppu + sprite.m_Rect.height*sprite.m_Pivot.y],
                  trim_size=[(x1-x0)*ppu, (y1-y0)*ppu], pivot=[-x0*ppu, -y0*ppu])
    if sprite.m_SpriteAtlas:
        atlas = sprite.m_SpriteAtlas.read()
        packed = next(value for key, value in atlas.m_RenderDataMap if key == sprite.m_RenderDataKey)
        rotation = (packed.settingsRaw >> 2) & 15
        if rotation:
            raise ValueError('A changed sprite needs its rotated atlas UV mapping: ' + sprite.m_Name)
        texture = packed.texture.read()
        rect = packed.textureRect
        result['atlas_uv'] = [[rect.x / texture.m_Width, 1 - (rect.y + rect.height) / texture.m_Height],
                              [rect.width / texture.m_Width, 0.0], [0.0, rect.height / texture.m_Height]]
        return result, image
    # Map normalized cropped-sprite coordinates to the original atlas UVs.
    uv = mesh.m_UV0
    for j in range(1, len(points)):
        for k in range(j+1, len(points)):
            a, b, c = points[0], points[j], points[k]
            dx, dy, ex, ey = b[0]-a[0], b[1]-a[1], c[0]-a[0], c[1]-a[1]
            determinant = dx*ey-dy*ex
            if abs(determinant) < 1e-10:
                continue
            axes = []
            for axis in (0, 1):
                du, eu = uv[j][axis]-uv[0][axis], uv[k][axis]-uv[0][axis]
                sx, sy = (du*ey-eu*dy)/determinant, (eu*dx-du*ex)/determinant
                axes.append((uv[0][axis] + sx*(x0-a[0]) + sy*(y0-a[1]), sx*(x1-x0), sy*(y1-y0)))
            result['atlas_uv'] = [[axes[0][n], axes[1][n]] for n in range(3)]
            return result, image
    raise ValueError('Official sprite has no nondegenerate UV triangle: ' + sprite.m_Name)


def close(a, b):
    if isinstance(a, list) and isinstance(b, list):
        return len(a) == len(b) and all(close(x, y) for x, y in zip(a, b))
    if isinstance(a, (int, float)) and isinstance(b, (int, float)):
        return abs(a-b) < 0.001
    return a == b


def build(do_activate=False):
    apks = [Apk(), Apk.current()]
    current = ROOT / '.tools/source-packages/current' / apks[1].version
    destination = ROOT / 'assets/reference/official-map/current-sprites-source-manifest.json'
    prior = activated_receipt(destination, apks[1].version)
    if prior:
        return prior
    discovery_path = current / 'decoded-images/source-manifest.json'
    images = [row for row in json.loads(discovery_path.read_bytes())['images'] if row['changed']]
    cache = {}
    definitions = {}
    inputs, outputs = [], []
    stage = current / 'sprite-payloads'
    stage.mkdir(exist_ok=True)

    def staged(path, raw, payload, **details):
        target = stage / (sha(payload) + path.suffix)
        target.write_bytes(payload)
        outputs.append(dict(path=path.relative_to(ROOT).as_posix(), previous_sha256=sha(raw),
                            sha256=sha(payload), bytes=len(payload),
                            staged=target.relative_to(ROOT).as_posix(), **details))

    for row in images:
        versions = []
        for number, apk in enumerate(apks):
            bundle = row['source']['bundle']
            key = number, bundle
            if key not in cache:
                cache[key] = {str(obj.path_id): obj for obj in apk.objects(bundle) if obj.type.name == 'Sprite'}
            obj = cache[key][row['source']['path_id']]
            data, image = metadata(obj)
            buffer = io.BytesIO(); image.save(buffer, format='PNG')
            digest = sha(buffer.getvalue())
            if digest != row['new_sha256' if number else 'old_sha256']:
                raise ValueError('Decoded Sprite differs from the recorded source: ' + row['uri'])
            versions.append(data)
            inputs.append({'source': apk.provenance(obj), 'raw_sha256': sha(obj.get_raw_data()),
                           'decoded_sha256': digest, 'metadata': data})
        path = ROOT / row['uri'].removeprefix('res://')
        raw, payload = path.read_bytes(), (ROOT / row['decoded_path']).read_bytes()
        if sha(raw) != row['old_sha256'] or sha(payload) != row['new_sha256']:
            raise ValueError('Sprite target or staged PNG changed: ' + row['uri'])
        staged(path, raw, payload)
        definitions[row['uri']] = versions

    command = ['rg', '--hidden', '--no-ignore', '--text', '-l', '-F']
    for uri in definitions:
        command += ['-e', uri.rsplit('/', 1)[-1]]
    command += ['assets/official', 'unity/Assets/Official', 'unity/Assets/StreamingAssets', '--glob', '*.json']
    found = subprocess.run(command, cwd=ROOT, capture_output=True, text=True, encoding='utf8', check=False)
    if found.returncode not in (0, 1):
        raise ValueError(found.stderr)
    for name in found.stdout.splitlines():
        path = ROOT / name
        if path.name.endswith('manifest.json'):
            continue
        raw = path.read_bytes(); document = json.loads(raw); changes = []

        def walk(value, location=''):
            if isinstance(value, list):
                for i, child in enumerate(value): walk(child, location + '/' + str(i))
            elif isinstance(value, dict):
                uri = value.get('file')
                if uri in definitions:
                    old, new = definitions[uri]
                    fields = []
                    for key in old:
                        if key not in value:
                            continue
                        if not close(value[key], old[key]):
                            raise ValueError('Sprite metadata differs from its source: ' + name + location + '/' + key)
                        if not close(old[key], new[key]):
                            value[key] = new[key]; fields.append(key)
                    if fields: changes.append({'pointer': location, 'sprite': uri, 'fields': fields})
                for key, child in value.items(): walk(child, location + '/' + key)

        walk(document)
        if changes:
            payload = json.dumps(document, ensure_ascii=False, separators=(',', ':')).encode('utf8')
            staged(path, raw, payload, changes=changes)
    result = {'manifest': apks[1].version, 'base_manifest': apks[0].version, 'operation': __doc__,
              'inputs': inputs, 'outputs': outputs, 'generator_sha256': sha(Path(__file__).read_bytes())}
    stage_manifest = stage / 'source-manifest.json'
    stage_manifest.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8')
    return activate(stage_manifest, Path(__file__), destination) if do_activate else result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--activate', action='store_true')
    print('Converted Sprite payloads:', len(build(parser.parse_args().activate)['outputs']))
