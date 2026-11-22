"""Extract current flame-momentum rules and the original report-parent mapping."""
import argparse
import json
import re
from pathlib import Path

from official_apk import Apk, ROOT
from official_tables import Constructors
from prepare_profession_skill_rule_pack import build, identity, sha


def prepare():
    raw, proof = build(10543)
    payload = json.loads(raw)
    with Apk.current() as apk:
        code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
        constructors = Constructors(code)
        mappings = {name.rsplit('/', 1)[-1]: cls for cls, name in
                    re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))}
        obj = apk.asset('config_luban', '/battle_report_skill_parent_override.g.bytes')
        data = obj.read().m_Script.encode('utf8', 'surrogateescape')
        payload['report_skill_parent_overrides'] = constructors.table(data, 'TbBattleReportSkillOverrideInfo')
        proof['inputs'].append(dict(source=apk.provenance(obj), payload_sha256=sha(data)))
        settings_obj = apk.asset('config_luban', '/game_settings.g.bytes')
        settings_raw = settings_obj.read().m_Script.encode('utf8', 'surrogateescape')
        settings = constructors.table(settings_raw, mappings['game_settings'])
        installed = json.loads((ROOT / 'unity/Assets/Official/Combat/rules.json').read_bytes())
        assert settings['ActiveSkillTypes'] == installed['game_settings']['ActiveSkillTypes'], 'Current active skill item types changed'
        proof['inputs'].append(dict(source=apk.provenance(settings_obj), payload_sha256=sha(settings_raw)))
        proof['active_skill_item_types'] = settings['ActiveSkillTypes']
        proof['report_parent_constructors'] = [identity(code / 'common-source'
            / ('Common' if (code / 'common-source/Common' / (name + '.cs')).is_file() else 'Common.LubanGen')
            / (name + '.cs')) for name in sorted(constructors.sources)]
    raw = (json.dumps(payload, ensure_ascii=False, separators=(',', ':')) + '\n').encode('utf8')
    proof['output_sha256'] = sha(raw)
    proof['common_generator'] = proof['generator']
    proof['generator'] = identity(Path(__file__))
    return raw, proof


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--write', action='store_true')
    args = parser.parse_args()
    raw, proof = prepare()
    target = ROOT / 'unity/Assets/Game/Resources/Combat/SkillRules/10543.json'
    receipt = target.with_name('10543-source-manifest.json')
    if args.write:
        target.write_bytes(raw)
        receipt.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    else:
        assert target.read_bytes() == raw and json.loads(receipt.read_bytes()) == proof
    print('Current flame-momentum rules and report skill parents verified.')


if __name__ == '__main__':
    main()
