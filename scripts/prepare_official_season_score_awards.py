"""Extract the pinned season score grade and reward table."""
import hashlib
import json

from official_apk import Apk, ROOT
from official_tables import Constructors, SOURCE


def save(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, separators=(',', ':')),
                    encoding='utf8')


def build():
    apk = Apk('854_156383')
    constructors = Constructors()
    asset_path, pointer = next((path, pointer) for path, pointer
                               in apk.container('config_luban_astrologicaldestiny')
                               if path.endswith('/destiny_score_award.g.bytes'))
    asset = pointer.deref()
    raw = asset.read().m_Script.encode('utf8', 'surrogateescape')
    constructor = 'TbDestinyScoreAwardInfo'
    rows = constructors.table(raw, constructor)
    if not rows:
        raise ValueError('Pinned season score award table is empty')

    data_path = ROOT / 'assets/official/season-lifecycle.json'
    manifest_path = ROOT / 'assets/reference/season-lifecycle/source-manifest.json'
    data = json.loads(data_path.read_text(encoding='utf8'))
    manifest = json.loads(manifest_path.read_text(encoding='utf8'))
    if manifest.get('manifest') != '854_156383':
        raise ValueError('Season lifecycle manifest is not pinned to 854_156383')
    data.setdefault('tables', {})['destiny_score_award'] = rows
    encoded = json.dumps(data, ensure_ascii=False, separators=(',', ':'),
                         allow_nan=False).encode('utf8')
    manifest.setdefault('tables', {})['destiny_score_award'] = dict(
        apk.provenance(asset), asset=asset_path, bytes=len(raw),
        sha256=hashlib.sha256(raw).hexdigest(), constructor=constructor,
        constructor_sha256=hashlib.sha256((SOURCE / (constructor + '.cs')).read_bytes()).hexdigest())
    manifest.setdefault('outputs', {})['season-lifecycle.json'] = hashlib.sha256(encoded).hexdigest()
    save(data_path, data)
    save(manifest_path, manifest)
    print('Pinned score award rows:', len(rows))


if __name__ == '__main__':
    build()
