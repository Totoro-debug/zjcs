"""Keep converted Unity asset identities reproducible across cache rebuilds."""
import hashlib
import json
from pathlib import Path
import re

from prepare_unity_profession_native_inputs import ROOT

GUID = re.compile(rb"(?m)^guid: ([0-9a-f]{32})\r?$")
REFERENCE = re.compile(rb"(?<=guid: )[0-9a-f]{32}")
TEXT = {'.prefab', '.asset', '.mat', '.meta', '.unity', '.anim',
        '.controller', '.overridecontroller', '.playable', '.rendertexture', '.spriteatlas'}


def restore(export):
    source = ROOT / 'assets/reference/profession-ui/native-guid-identities.json'
    data = json.loads(source.read_bytes())
    if data['manifest'] != '871_156598':
        raise ValueError('Unexpected profession native identity version')
    retained = data['identities']
    remap, identities = {}, {}
    for meta in sorted(export.rglob('*.meta')):
        raw = meta.read_bytes()
        match = GUID.search(raw)
        if not match:
            raise ValueError('Missing exported asset GUID: ' + str(meta))
        relative = meta.with_suffix('').relative_to(export).as_posix()
        previous = match[1].decode('ascii')
        stable = retained.get(relative, hashlib.md5(
            ('zjcs:profession:871_156598:' + relative).encode()).hexdigest())
        if previous in remap or stable in identities.values():
            raise ValueError('Duplicate profession native identity: ' + relative)
        remap[previous] = stable
        identities[relative] = stable
    missing = retained.keys() - identities.keys()
    if missing:
        raise ValueError('Published original asset disappeared: ' + repr(sorted(missing)))
    for entry in data.get('embedded_references', []):
        raw = (export / entry['asset']).read_bytes()
        matches = re.findall(entry['pattern'].encode(), raw)
        if len(matches) != 1:
            raise ValueError('Original embedded asset reference differs: ' + entry['asset'])
        previous = matches[0].decode('ascii')
        if previous in remap and remap[previous] != entry['guid']:
            raise ValueError('Embedded/exported identity conflict: ' + entry['asset'])
        remap[previous] = entry['guid']
    encoded = {key.encode(): value.encode() for key, value in remap.items()}
    for path in sorted(export.rglob('*')):
        if not path.is_file() or path.suffix.lower() not in TEXT:
            continue
        raw = path.read_bytes()
        replacement = REFERENCE.sub(lambda match: encoded.get(match[0], match[0]), raw)
        if replacement != raw:
            path.write_bytes(replacement)
    return dict(source=dict(path=source.relative_to(ROOT).as_posix(),
                           sha256=hashlib.sha256(source.read_bytes()).hexdigest()),
                tool=dict(path=Path(__file__).relative_to(ROOT).as_posix(),
                          sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest()),
                asset_count=len(identities), retained_count=len(retained),
                operation='Preserve published native asset GUIDs; derive remaining conversion GUIDs from source version and export relative path. Rewrite exact GUID fields consistently before original-field verification; no original numeric, hierarchy or animation values change.')
