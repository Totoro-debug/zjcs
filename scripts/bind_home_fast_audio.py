"""Bind the already verified complete original bed acceleration PCM to its owner."""
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
RECEIPT = ROOT / 'assets/reference/home-hangup-windows-native-source-manifest.json'
CATALOG = ROOT / 'unity/Assets/Game/Resources/Home/OfficialHomeHangUpWindowAssetCatalog.asset'
SCRIPT = ROOT / 'unity/Assets/Game/Runtime/Official/HomeHangUpWindowsUI/OfficialHomeHangUpWindowAssetCatalog.cs'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def bind():
    proof = json.loads(RECEIPT.read_bytes())
    audio, = [row for row in proof['audio'].values() if row['event']['id'] == 3729877424]
    candidates = list(proof['installed_output_sha256']) + [row['path'] for row in proof['reused']]
    paths = {name for name in candidates if name.endswith('/' + audio['output'])}
    original, = paths
    path = ROOT / original
    if sha(path.read_bytes()) != audio['output_sha256']:
        raise ValueError('Original layered acceleration PCM changed')
    guid = re.search(r'^guid: ([0-9a-f]{32})$', Path(str(path) + '.meta').read_text('utf8'), re.M).group(1)
    if guid != audio['pointer']['guid']:
        raise ValueError('Original acceleration PCM binding differs')
    raw = CATALOG.read_bytes()
    if sha(raw) != proof['installed_output_sha256'][CATALOG.relative_to(ROOT).as_posix()]:
        raise ValueError('Preserve changed native bed catalog')
    text = raw.decode('utf8')
    text = re.sub(r'^  fastHangUp(?:EventId|Clip):.*\n', '', text, flags=re.M)
    text += '  fastHangUpEventId: ' + str(audio['event']['id']) + '\n'
    p = audio['pointer']
    text += '  fastHangUpClip: {fileID: ' + str(p['fileID']) + ', guid: ' + guid + ', type: ' + str(p['type']) + '}\n'
    CATALOG.write_text(text, 'utf8', newline='\n')
    proof['installed_output_sha256'][CATALOG.relative_to(ROOT).as_posix()] = sha(CATALOG.read_bytes())
    proof['runtime_code'][SCRIPT.relative_to(ROOT).as_posix()] = sha(SCRIPT.read_bytes())
    proof['production_audio_binding'] = dict(
        operation='Post-install explicit binding of the existing verified complete original acceleration PCM; no re-encoding.',
        generator=Path(__file__).relative_to(ROOT).as_posix(), generator_sha256=sha(Path(__file__).read_bytes()),
        pcm=dict(path=original, sha256=sha(path.read_bytes()), guid=guid),
        catalog=CATALOG.relative_to(ROOT).as_posix(), catalog_sha256=sha(CATALOG.read_bytes()))
    RECEIPT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Bound original bed acceleration event:', audio['event']['id'])


if __name__ == '__main__':
    bind()
