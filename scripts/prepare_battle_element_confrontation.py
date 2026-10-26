"""Verify the original elemental-resistance states and their shared formula inputs."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from official_apk import Apk, ROOT
from official_ec_codec import Formats
from official_map_codec import unpack


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def build(output_root=ROOT):
    payload_path = ROOT / 'unity/Assets/Official/Combat/battle-rules.json'
    payload = json.loads(payload_path.read_bytes())
    wanted = set(range(25051, 25061))
    versions = []
    for version in ('854_156383', '871_156598'):
        apk, found, inputs = Apk(manifest=version), {}, []
        for path, pointer in apk.container('config_ec'):
            if path.rsplit('/', 1)[-1] != 'FightSkill_part753.g.bytes':
                continue
            obj = pointer.deref()
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            try:
                rows = unpack(raw)[0]
            except Exception:
                rows = unpack(raw[2:])[0]
            provenance = apk.provenance(obj)
            provenance.update(file=path, payload_sha256=hashlib.sha256(raw).hexdigest())
            inputs.append(provenance)
            for parent, identity, components in rows:
                if identity not in wanted:
                    continue
                definition = {}
                for typ, value in components:
                    name = typ.removeprefix('Common.')
                    if name in ('FightStatusDamageElementComponent', 'FightStatusComponent', 'ActionComponent'):
                        definition[name] = Formats().component(typ, value)
                        if definition[name] != payload['entities'][str(identity)][name]:
                            raise ValueError(f'Official {version} differs from runtime: {identity}/{name}')
                if len(definition) != 3:
                    raise ValueError('Incomplete original elemental-status definition')
                found[str(identity)] = {'parent': parent, 'components': definition}
        if len(found) != len(wanted) or len(inputs) != 1:
            raise ValueError('Incomplete original elemental-status source coverage: ' + version)
        versions.append({'manifest': version, 'inputs': inputs, 'entities': found})

    code = ['FightStatusDamageElementComponent.cs', 'FightStatusDamageElementComponentInfo.cs',
            'DamageResultParameter.cs', 'BattleFormulaHandler.cs', 'ElementType.cs']
    code_inputs = []
    for version, directory in (
        ('854_156383', ROOT / '.tools/source-packages/official-code'),
        ('871_156598', ROOT / '.tools/source-packages/current/871_156598/official-code')):
        code_inputs.append({'manifest': version, 'files': [
            {'path': (directory/'common-source/Common'/name).relative_to(ROOT).as_posix(),
             'sha256': sha(directory/'common-source/Common'/name)} for name in code]})
    runtime = ['unity/Assets/Game/Runtime/Official/Combat/'+name for name in (
        'FightOfficialCombatCatalog.ElementConfrontation.cs', 'FightBattleExecutor.cs',
        'FightBattleStatistics.cs', 'FightDamageFormula.cs', 'FightBattleHitExecutor.cs')]
    runtime += ['scripts/test_battle_element_confrontation.ps1',
                'unity/Assets/Game/Editor/Combat/OfficialBattleElementConfrontationAcceptance.cs']
    receipt = {
        'config_manifest': '854_156383', 'versions': versions, 'official_code': code_inputs,
        'runtime_config_identical_in_both_versions': True,
        'runtime_config': {'path': payload_path.relative_to(ROOT).as_posix(), 'sha256': sha(payload_path)},
        'generator_sha256': sha(__file__),
        'runtime': [{'path': path, 'sha256': sha(output_root/path)} for path in runtime],
        'operation': 'Supply the original float Value for a single matching status. For multiple statuses, follow the user-confirmed offline rule: an elemental-immunity entry wins; otherwise multiply the 1-Value damage factors, then pass their combined value to the existing formula. Preserve integer conversion, pre-shield settlement, result fields and checkpoint replay.',
        'confirmed_offline_rules': {
            'immunity_priority': 'A matching elemental-immunity state completely prevents damage of that element, including simultaneous weakness states. Do not consume shield.',
            'other_states': 'Multiply the damage factors of matching statuses: final factor = product(1-Value). Preserve the configured single-entry float and the existing formula truncation. This confirmation does not specify scaling per status stack.'},
        'evidence_boundary': 'The original component configurations and shared formula are recovered. The server binding and state combiner are absent; their local wiring and aggregation follow the user-confirmed rule. Historical unresolved receipts retain their original record. Direct fixed damage, shield-consumption overrides and interactions with other custom hooks are not claimed as restored. The separate battle-hud/element-feedback-source-manifest.json records the restored six world element labels, materials and animation bindings.'}
    target = output_root/'assets/reference/battle-rules/element-confrontation-source-manifest.json'
    target.write_bytes((json.dumps(receipt, ensure_ascii=False, indent=2)+'\n').encode())
    print('Verified ten elemental-resistance statuses / twenty entries in both official versions.')


if __name__ == '__main__':
    build()
