"""Preserve the official appearance placeholder name when parsing native atlases."""
import hashlib
import json
from pathlib import Path

from prepare_battle_scene_appearance_runtime import write_changed

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / '.tools/source-packages/official-code/client-source/AppearanceSystem.Runtime/AppearanceOnDemandTextureLoader.cs'
RUNTIME = ROOT / 'unity/Assets/Spine/Runtime/spine-unity/Asset Types/SpineAtlasAsset.cs'
RECEIPT = ROOT / 'assets/reference/battle-animation/atlas-placeholder-binding-source-manifest.json'
SOURCE_SHA256 = '2b3619bb979fce0483e373c14bd8854c2848a228d0ab5743695815da7eda7463'
BEFORE_SHA256 = 'c5fa815e0d9015e3fbc7e7737f0e08e248c0706404dd35aa169fb7b7893dd858'
AFTER_SHA256 = '1b4f8140bb0912e5e57143a00948d79d4dadd8d9fae67ed7402a3d623efbfb9d'
CANONICAL_BEFORE_SHA256 = '81d6e94c9b37647613a1bc9561191b32b8264225b7a5bfda22f0b61d7de995b2'
CANONICAL_AFTER_SHA256 = 'f26d2c7795f944a0cb031ba46f6efccb4f68c47431be1e64020b121f8f82dd4a'
BEFORE = '\t\t\t\tif (textureName == name ||\n'
AFTER = ('\t\t\t\t// Official AppearanceOnDemandTextureLoader.GetPlaceholderTextureName:\n'
         '\t\t\t\t// exported atlases can be parsed before their appearance loader is bound.\n'
         '\t\t\t\tif (textureName == name ||\n'
         '\t\t\t\t\t(atlasAsset.OnDemandTextureLoader == null && textureName == name + "_placeholder") ||\n')


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def main():
    selection = ROOT / 'official-source.json'
    if json.loads(selection.read_text(encoding='utf-8'))['active'] != '884_156692':
        raise ValueError('Review the selected official appearance source version.')
    original = SOURCE.read_bytes()
    if sha(original) != SOURCE_SHA256:
        raise ValueError('Review the official appearance loader source identity.')
    if 'return originalTextureName + "_placeholder";' not in original.decode('utf-8-sig'):
        raise ValueError('The official placeholder name rule differs.')
    raw = RUNTIME.read_bytes()
    text = raw.decode('utf-8').replace('\r\n', '\n')
    canonical = sha(text.encode('utf-8'))
    if canonical == CANONICAL_BEFORE_SHA256:
        if text.count(BEFORE) != 1:
            raise ValueError('Native atlas material lookup is ambiguous.')
        newline = '\r\n' if b'\r\n' in raw else '\n'
        converted = text.replace(BEFORE, AFTER).replace('\n', newline).encode('utf-8')
        if sha(converted.replace(b'\r\n', b'\n')) != CANONICAL_AFTER_SHA256:
            raise ValueError('Review native atlas conversion output identity.')
        RUNTIME.write_bytes(converted)
    elif canonical != CANONICAL_AFTER_SHA256:
        raise ValueError('Refusing to overwrite changed native atlas loader.')
    outputs = [dict(path=RUNTIME.relative_to(ROOT).as_posix(), sha256=sha(RUNTIME.read_bytes()))]
    graph_receipt = ROOT / 'assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json'
    graphs = json.loads(graph_receipt.read_text(encoding='utf-8'))
    updated = 0
    for application in graphs['application']:
        for key in ('migrated', 'migrated_script'):
            row = application.get(key, {})
            if row.get('path') == outputs[0]['path']:
                if row['sha256'] not in (BEFORE_SHA256, AFTER_SHA256,
                                         CANONICAL_BEFORE_SHA256, CANONICAL_AFTER_SHA256):
                    raise ValueError('Review the recorded native atlas script identity.')
                row['sha256'] = outputs[0]['sha256']
                updated += 1
    if updated != 2:
        raise ValueError('Expected the two existing skill graph atlas script bindings.')
    write_changed(graph_receipt, json.dumps(graphs, ensure_ascii=False, indent=2) + '\n')
    outputs.append(dict(path=graph_receipt.relative_to(ROOT).as_posix(), sha256=sha(graph_receipt.read_bytes())))
    proof = dict(manifest='884_156692',
        official_source=dict(path=selection.relative_to(ROOT).as_posix(), sha256=sha(selection.read_bytes())),
        source=dict(path=SOURCE.relative_to(ROOT).as_posix(), sha256=SOURCE_SHA256,
                    method='AppearanceOnDemandTextureLoader.GetPlaceholderTextureName'),
        native_loader_input=dict(path=RUNTIME.relative_to(ROOT).as_posix(), sha256=BEFORE_SHA256,
                                 lf_sha256=CANONICAL_BEFORE_SHA256),
        outputs=outputs,
        generator=dict(path=Path(__file__).relative_to(ROOT).as_posix(), sha256=sha(Path(__file__).read_bytes())),
        binding='MaterialsTextureLoader.Load matches the exact existing official placeholder material when the appearance loader is not yet bound; explicit loaders retain their own mapping.',
        payload_changes='None; original atlas, materials, texture pixels, names and dimensions are preserved.')
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    write_changed(RECEIPT, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    print('Recorded official native atlas placeholder material binding.')


if __name__ == '__main__':
    main()
