"""Verify explicitly declared skill-rule dependencies before composing their entity baseline."""
import copy
import hashlib
import json


def read_verified_rule_pack(root, relative):
    path = (root / relative).resolve()
    assert path.is_relative_to((root / 'unity/Assets/Game/Resources/Combat/SkillRules').resolve())
    receipt = path.with_name(path.stem + '-source-manifest.json')
    raw = path.read_bytes()
    pack, proof = json.loads(raw), json.loads(receipt.read_bytes())
    assert isinstance(pack['manifest'], str) and pack['manifest'] and pack['item_id'] > 0
    assert hashlib.sha256(raw).hexdigest() == proof['output_sha256'], ('Baseline rule pack payload changed', relative)
    assert pack['manifest'] == proof['manifest'] and pack['item_id'] == proof['item_id'], ('Rule pack source identity differs', relative)
    assert pack['entities'] and pack['entities'].keys() == pack['previous_entities'].keys()
    return path, receipt, pack, proof


def apply_rule_pack_baseline(entities, pack, label):
    for key, expected in pack['previous_entities'].items():
        assert entities.get(key) == expected, ('Skill rule pack baseline changed', label, key)
    for key, value in pack['entities'].items():
        entities[key] = copy.deepcopy(value)
