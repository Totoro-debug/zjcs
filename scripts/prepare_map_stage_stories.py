"""Add the original battle story table without rebuilding unrelated map data."""
import hashlib
import json
from official_apk import Apk, ROOT
from official_tables import Constructors, SOURCE
from prepare_map_regions import save, ART, REF


def build():
    apk = Apk()
    obj = apk.asset('defaultpackage_assets_config_luban.bundle', '/stage_story.g.bytes')
    raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
    rows = Constructors().table(raw, 'TbStageStoryInfo')
    adventure = json.loads((ART / 'adventure.json').read_text(encoding='utf8'))
    adventure['tables']['stage_story'] = rows
    save(ART / 'adventure.json', adventure)
    digest = hashlib.sha256((ART / 'adventure.json').read_bytes()).hexdigest()
    receipt = json.loads((REF / 'story-source-manifest.json').read_text(encoding='utf8'))
    receipt['tables']['stage_story'] = dict(apk.provenance(obj), bytes=len(raw), consumed_bytes=len(raw),
        sha256=hashlib.sha256(raw).hexdigest(), constructor='TbStageStoryInfo',
        constructor_sha256=hashlib.sha256((SOURCE / 'TbStageStoryInfo.cs').read_bytes()).hexdigest())
    receipt['adventure_sha256'] = digest
    save(REF / 'story-source-manifest.json', receipt)
    manifest_path = ROOT / 'assets/reference/official-city/source-manifest.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf8'))
    manifest['data_sha256']['adventure.json'] = digest
    save(manifest_path, manifest)
    print('Native stage stories:', len(rows), 'rows; bytes:', len(raw))


if __name__ == '__main__':
    build()
