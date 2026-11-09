"""Resolve original hit view fields without inferring shared/server hit creation."""
import json
import re
import struct
import sys
from collections import defaultdict
from hashlib import sha256
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
try:
    from Crypto.Hash import MD4
except ModuleNotFoundError:
    sys.path.insert(0, str(ROOT / '.tools/character-art-python'))
    from Crypto.Hash import MD4

from inspect_battle_skill_graph_bindings import EXPORT, EXPORT_RECEIPT

CLIENT = ROOT / '.tools/source-packages/current/884_156692/official-code/client-source'
NAMES = ('Fixed', 'Summon', 'RandomTarget', 'CustomTarget', 'TriggerSkill', 'Chained')
FIELDS = ('myPlayable', 'myScopePlayable', 'SourceToMePlayable', 'myHitToNextHitPlayable')
FLAGS = dict(ScopePlayableFollowDelay=True, ScopeZFollowHitZ=False,
             ShouldSetPlayableDirectinParamToSouceToMePlayable=False,
             IsApplyOffsetToPlayable=False)


def inspect():
    receipt = json.loads(EXPORT_RECEIPT.read_text(encoding='utf-8'))
    if receipt['manifest'] != '884_156692':
        raise ValueError('Review the native hit input version.')
    verified = {row['path']: row['sha256'] for row in receipt['outputs']}
    scripts = {}
    sources = []
    for name in ('MyFightHitBase', 'EFightHitSourceToMeAnchor'):
        path = CLIENT / (name + '.cs')
        sources.append(dict(path=path.relative_to(ROOT).as_posix(),
                            sha256=sha256(path.read_bytes()).hexdigest()))
    base = (CLIENT / 'MyFightHitBase.cs').read_text(encoding='utf-8-sig')
    for name, default in FLAGS.items():
        declaration = re.search(rf'public bool {name}(?: = (true|false))?;', base)
        if not declaration or (declaration[1] == 'true') != default:
            raise ValueError('Review original hit flag default: ' + name)
    for suffix in NAMES:
        name = 'MyFightHit' + suffix
        path = CLIENT / (name + '.cs')
        text = path.read_text(encoding='utf-8-sig')
        if re.search(r'(?m)^namespace ', text) or ('public class ' + name) not in text:
            raise ValueError('Review original hit class identity: ' + name)
        file_id = struct.unpack('<i', MD4.new(('s\0\0\0' + name).encode()).digest()[:4])[0]
        scripts[file_id] = name
        sources.append(dict(path=path.relative_to(ROOT).as_posix(),
                            sha256=sha256(path.read_bytes()).hexdigest(), file_id=file_id))
    paths = defaultdict(list)
    for metadata in EXPORT.rglob('*.meta'):
        match = re.search(r'(?m)^guid: ([a-f0-9]{32})$', metadata.read_text(encoding='utf-8-sig'))
        if match:
            paths[match[1]].append(metadata.with_suffix('').relative_to(EXPORT).as_posix())
    hits = []
    for path in sorted(EXPORT.glob('[0-9]*/[0-9]*.prefab')):
        raw = path.read_bytes()
        digest = sha256(raw).hexdigest()
        if digest != verified[path.relative_to(EXPORT.parents[5]).as_posix()]:
            raise ValueError('Changed native hit entity input: ' + str(path))
        text = raw.decode('utf-8-sig').replace('\r\n', '\n')
        entity_id = int(path.stem)
        if not re.search(rf'(?m)^  m_Name: {entity_id}$', text):
            raise ValueError('Original hit entity name differs: ' + str(path))
        for component in re.finditer(
                r'(?ms)^--- !u!114 &(?P<id>\d+)\nMonoBehaviour:\n(?P<body>.*?)(?=^--- !u!|\Z)', text):
            body = component['body']
            script = re.search(r'(?m)^  m_Script: \{fileID: (-?\d+),', body)
            if not script or int(script[1]) not in scripts:
                continue
            refs = {}
            for field in FIELDS:
                ref = re.search(rf'(?m)^  {field}: \{{fileID: (\d+), guid: ([a-f0-9]{{32}}), type: (\d+)\}}$', body)
                if ref:
                    candidates = paths.get(ref[2], [])
                    if len(candidates) != 1:
                        raise ValueError(f'Unresolved original hit graph {entity_id}:{field}: {candidates}')
                    metadata = EXPORT / (candidates[0] + '.meta')
                    if sha256(metadata.read_bytes()).hexdigest() != verified[
                            metadata.relative_to(EXPORT.parents[5]).as_posix()]:
                        raise ValueError('Changed original hit graph metadata: ' + str(metadata))
                    refs[field] = dict(file_id=int(ref[1]), guid=ref[2], type=int(ref[3]), path=candidates[0])
            flags = {}
            for field, default in FLAGS.items():
                value = re.search(rf'(?m)^  {field}: ([01])$', body)
                flags[field] = bool(int(value[1])) if value else default
            anchor = re.search(r'(?m)^  SourceToMeAnchor: (\d+)$', body)
            enabled = re.search(r'(?m)^  m_Enabled: ([01])$', body)
            game_object = re.search(r'(?m)^  m_GameObject: \{fileID: (\d+)\}$', body)
            hits.append(dict(hit_entity_id=entity_id, client_type=scripts[int(script[1])],
                component_file_id=int(component['id']), game_object_file_id=int(game_object[1]),
                component_enabled=bool(int(enabled[1])), source_to_me_anchor=int(anchor[1]) if anchor else 0,
                flags=flags, playables=refs, component_sha256=sha256(body.encode()).hexdigest(),
                entity_prefab=path.relative_to(EXPORT).as_posix(), entity_prefab_sha256=digest))
    return dict(manifest=receipt['manifest'], sources=sources,
        native_export_receipt=dict(path=EXPORT_RECEIPT.relative_to(ROOT).as_posix(),
            sha256=sha256(EXPORT_RECEIPT.read_bytes()).hexdigest()), hits=hits)


if __name__ == '__main__':
    print(json.dumps(inspect(), ensure_ascii=False, separators=(',', ':')))
