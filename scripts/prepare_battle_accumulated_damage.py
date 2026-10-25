"""Verify the official 24062 -> 13148 -> 13149 accumulated-damage dependency chain."""
import hashlib
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from official_apk import Apk, ROOT
from official_ec_codec import Formats
from official_map_codec import unpack

WANTED = {24062: 'FightStatusAccDmgAndCastCustomDamageComponent',
          13148: 'FightSkillComponent', 13149: 'FightHitFixedComponent'}


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def build():
    payload_path = ROOT / 'unity/Assets/Official/Combat/battle-rules.json'
    payload = json.loads(payload_path.read_text('utf8'))
    versions = []
    for version in ('854_156383', '871_156598'):
        apk = Apk(manifest=version)
        found, sources = {}, []
        for path, pointer in apk.container('config_ec'):
            if path.rsplit('/', 1)[-1] not in ('FightSkill_part157.g.bytes', 'FightSkill_part703.g.bytes'):
                continue
            obj = pointer.deref()
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            try:
                rows = unpack(raw)[0]
            except Exception:
                rows = unpack(raw[2:])[0]
            provenance = apk.provenance(obj)
            provenance.update(file=path, payload_sha256=hashlib.sha256(raw).hexdigest())
            sources.append(provenance)
            for parent, identity, components in rows:
                if identity not in WANTED:
                    continue
                for typ, value in components:
                    if typ.removeprefix('Common.') == WANTED[identity]:
                        decoded = Formats().component(typ, value)
                        if decoded != payload['entities'][str(identity)][WANTED[identity]]:
                            raise ValueError(f'Official {version} configuration differs from runtime: {identity}')
                        found[str(identity)] = {'parent': parent, 'component': WANTED[identity], 'value': decoded}
        if len(found) != len(WANTED):
            raise ValueError('Incomplete official accumulated-damage dependency chain: ' + version)
        versions.append({'manifest': version, 'inputs': sources, 'entities': found})
    text_path = ROOT / 'unity/Assets/Official/Combat/battle-texts.json'
    texts = json.loads(text_path.read_text('utf8'))
    description = next(row for row in texts['texts'] if row['key'] == 'entry_24062_desc')
    source_paths = [
        'unity/Assets/Official/Combat/battle-rules.json', 'unity/Assets/Official/Combat/battle-texts.json',
        '.tools/source-packages/current/871_156598/official-code/common-source/Common/FightStatusAccDmgAndCastCustomDamageComponentInfo.cs',
        '.tools/source-packages/current/871_156598/official-code/common-source/Common/FightStatusTriggerSkillCfg.cs',
        '.tools/source-packages/current/871_156598/official-code/common-source/Common/HitDamageType.cs',
        '.tools/source-packages/current/871_156598/official-code/common-source/Common/DamageResultType.cs']
    receipt = {'config_manifest': '854_156383', 'code_manifest': '871_156598',
               'language_manifest': texts['language_manifest'], 'versions': versions,
               'runtime_config_identical_in_both_versions': True,
               'description': description,
               'inputs': [{'path': path, 'sha256': sha(ROOT / path)} for path in source_paths],
               'generator_sha256': sha(__file__),
               'scope': 'Accumulate actual HP loss excluding true damage, invoke configured custom-damage skill at status end. Server execution and source-statistics allocation are not present in these inputs.'}
    target = ROOT / 'assets/reference/battle-rules/accumulated-damage-source-manifest.json'
    target.write_bytes((json.dumps(receipt, ensure_ascii=False, indent=2) + '\n').encode('utf8'))
    print('Verified official 24062 -> 13148 -> 13149 in both manifests; runtime configuration is identical.')


if __name__ == '__main__':
    build()
