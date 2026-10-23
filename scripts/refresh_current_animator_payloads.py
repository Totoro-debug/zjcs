"""Refresh changed official Animator graphs in the existing extracted catalog."""
import hashlib
import json
from pathlib import Path

from activate_current_payloads import activate
from current_conversion_state import activated_receipt
from official_apk import Apk, ROOT


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def controller(apk, obj):
    tree = obj.read_typetree()
    clips = []
    for pointer in obj.read().m_AnimationClips:
        if not pointer:
            clips.append({})
            continue
        clip = pointer.deref().read_typetree()
        timing = clip.get('m_MuscleClip', {})
        clips.append({'name': clip['m_Name'],
                      'duration': float(timing.get('m_StopTime', 1))-float(timing.get('m_StartTime', 0)),
                      'loop': bool(timing.get('m_LoopTime', False)),
                      'sample_rate': float(clip.get('m_SampleRate', 30))})
    return {'name': tree['m_Name'], 'graph': tree['m_Controller'],
            'strings': {str(k): v for k, v in tree['m_TOS']}, 'clips': clips,
            'source': apk.provenance(obj)}


def build():
    apks = [Apk(), Apk.current()]
    destination = ROOT / 'assets/reference/official-map/current-animators-source-manifest.json'
    prior = activated_receipt(destination, apks[1].version)
    if prior:
        return prior
    cache = ROOT / '.tools/source-packages/current' / apks[1].version
    path = ROOT / 'assets/official/battle-animation/catalog.json'
    raw = path.read_bytes(); document = json.loads(raw)
    changes, inputs = [], []
    rows = json.loads((cache / 'referenced-object-comparison.json').read_bytes())
    for row in rows:
        if row.get('type') != 'AnimatorController' or row.get('equal'):
            continue
        objects = [next(obj for obj in apk.objects(row['bundle']) if str(obj.path_id) == str(row['path_id'])) for apk in apks]
        old, new = [controller(apk, obj) for apk, obj in zip(apks, objects)]
        key = objects[0].assets_file.name + ':' + str(objects[0].path_id)
        if key not in document['controllers']:
            continue
        before = document['controllers'][key]
        # Sources may already carry supplementary provenance; the graph and
        # clip values must still agree exactly with their original objects.
        if any(before.get(field) != old[field] for field in ('name', 'graph', 'strings', 'clips')):
            raise ValueError('Animator catalog differs from its official base: ' + key)
        document['controllers'][key] = new
        changes.append({'key': key, 'bundle': row['bundle'], 'path_id': row['path_id']})
        inputs += [{'source': apk.provenance(obj), 'raw_sha256': sha(obj.get_raw_data())} for apk, obj in zip(apks, objects)]
    payload = json.dumps(document, ensure_ascii=False, separators=(',', ':')).encode('utf8')
    stage = cache / 'animator-payloads'; stage.mkdir(exist_ok=True)
    staged = stage / (sha(payload)+'.json'); staged.write_bytes(payload)
    result = {'manifest': apks[1].version, 'base_manifest': apks[0].version, 'operation': __doc__,
              'inputs': inputs, 'outputs': [{'path': path.relative_to(ROOT).as_posix(),
                  'previous_sha256': sha(raw), 'sha256': sha(payload), 'bytes': len(payload),
                  'staged': staged.relative_to(ROOT).as_posix(), 'changes': changes}],
              'generator_sha256': sha(Path(__file__).read_bytes())}
    manifest = stage / 'source-manifest.json'; manifest.write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n','utf8')
    return activate(manifest, Path(__file__), destination)


if __name__ == '__main__':
    print('Converted Animator catalogs:', len(build()['outputs']))
