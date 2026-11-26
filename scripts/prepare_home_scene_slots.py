"""Extract the official main scene's outdoor furniture Transform hierarchy."""
import hashlib
import json
from pathlib import Path

from official_apk import Apk, ROOT


DEST = ROOT / 'unity/Assets/Game/Resources/Home/native-scene-slots.json'
RECEIPT = ROOT / 'assets/reference/home-scene-slots-source-manifest.json'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def prepare():
    apk = Apk.current()
    proofs = []
    try:
        objects = apk.objects('defaultpackage_assets_scenes_main.bundle')
        roots = [obj for obj in objects if obj.type.name == 'GameObject'
                 and obj.read().m_Name == 'NormalFurniture']
        root, = roots
        transform, = [pair.component.deref() for pair in root.read().m_Component
                      if pair.component.deref().type.name == 'Transform']
        if transform.read().m_Father.read().m_GameObject.read().m_Name != 'Home':
            raise ValueError('The original outdoor slot parent differs')

        def node(obj):
            value = obj.read()
            go = value.m_GameObject.deref()
            game = go.read()
            for original in (obj, go):
                proofs.append(dict(source=apk.provenance(original), type=original.type.name,
                                   sha256=sha(original.get_raw_data())))
            return dict(name=game.m_Name,
                        position=[getattr(value.m_LocalPosition, key) for key in 'xyz'],
                        rotation=[getattr(value.m_LocalRotation, key) for key in 'xyzw'],
                        scale=[getattr(value.m_LocalScale, key) for key in 'xyz'],
                        children=[node(child.deref()) for child in value.m_Children])

        data = dict(root=node(transform))
        DEST.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        meta = Path(str(DEST) + '.meta')
        if not meta.exists():
            token = hashlib.md5(b'zjcs:home-native-scene-slots').hexdigest()
            meta.write_text('fileFormatVersion: 2\nguid: ' + token
                            + '\nTextScriptImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
        inputs = [Path(__file__), ROOT / 'scripts/official_apk.py', ROOT / 'scripts/official_source.py']
        receipt = dict(manifest=apk.version, source_objects=proofs,
                       inputs={p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in inputs},
                       outputs={p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in (DEST, meta)},
                       scope='Exact outdoor furniture slot Transform subtree under the original Home scene; no building completion assertion.')
        RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Prepared original outdoor furniture slots:', len(data['root']['children']))
    finally:
        apk.reader.close()


if __name__ == '__main__':
    prepare()
