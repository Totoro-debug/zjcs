"""Exercise declared dependency identity, payload integrity, order and atomic baseline composition."""
from pathlib import Path
import copy
import hashlib
import json
import uuid

from profession_skill_rule_baselines import read_verified_rule_pack, apply_rule_pack_baseline


def main():
    root = Path(__file__).resolve().parents[1] / 'output/validation' / ('skill-rule-baselines-' + uuid.uuid4().hex)
    folder = root / 'unity/Assets/Game/Resources/Combat/SkillRules'
    folder.mkdir(parents=True)
    old = {'10004': {'value': 1}}
    first = dict(manifest='verified-historical-source', item_id=10098,
        entities={'10004': {'value': 2}}, previous_entities=old)
    second = dict(manifest='verified-current-source', item_id=10574,
        entities={'10004': {'value': 3}}, previous_entities=first['entities'])

    def write(item, pack):
        path = folder / (str(item) + '.json')
        raw = (json.dumps(pack, sort_keys=True) + '\n').encode()
        path.write_bytes(raw)
        proof = dict(manifest=pack['manifest'], item_id=pack['item_id'], output_sha256=hashlib.sha256(raw).hexdigest())
        receipt = path.with_name(path.stem + '-source-manifest.json')
        receipt.write_bytes((json.dumps(proof) + '\n').encode())
        return path, receipt

    def reject(action):
        try:
            action()
        except AssertionError:
            return
        raise AssertionError('The invalid dependency was accepted.')

    path, receipt = write(10098, first)
    relative = path.relative_to(root)
    _, _, verified, _ = read_verified_rule_pack(root, relative)
    state = copy.deepcopy(old)
    apply_rule_pack_baseline(state, verified, 'first')
    assert state == first['entities']
    verified['entities']['10004']['value'] = 99
    assert state == first['entities'], 'Composed state aliases mutable source data.'
    write(10574, second)
    _, _, next_pack, _ = read_verified_rule_pack(root, (folder / '10574.json').relative_to(root))
    reject(lambda: apply_rule_pack_baseline(copy.deepcopy(old), next_pack, 'out of order'))
    apply_rule_pack_baseline(state, next_pack, 'second')
    assert state == second['entities']
    before = copy.deepcopy(state)
    reject(lambda: apply_rule_pack_baseline(state, next_pack, 'repeated baseline'))
    assert state == before, 'Failed baseline verification partially changed installed entities.'
    path.write_bytes(path.read_bytes() + b' ')
    reject(lambda: read_verified_rule_pack(root, relative))
    path, receipt = write(10098, first)
    proof = json.loads(receipt.read_bytes()); proof['manifest'] = 'another-source'
    receipt.write_bytes(json.dumps(proof).encode())
    reject(lambda: read_verified_rule_pack(root, relative))
    path, receipt = write(10098, first)
    proof = json.loads(receipt.read_bytes()); proof['item_id'] = 10099
    receipt.write_bytes(json.dumps(proof).encode())
    reject(lambda: read_verified_rule_pack(root, relative))
    reject(lambda: read_verified_rule_pack(root, '../outside.json'))
    (root / 'result.json').write_bytes(b'{"passed":true}\n')
    print('Rule pack dependency integrity, source/item identity, historical source, order, atomic rejection and copying passed.')


if __name__ == '__main__':
    main()
