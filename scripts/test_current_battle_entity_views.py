"""Check managed-reference graph integrity without opening an official input."""
import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from current_battle_entity_views import CurrentBattleEntityViews


def record(rid, name, data):
    return dict(rid=rid, type=dict(asm='Assembly-CSharp', ns='', **{'class': name}), data=data)


def fixture():
    return dict(mECViews=[dict(rid=10), dict(rid=20)], references=dict(version=2, RefIds=[
        record(40, 'StatusInvokedArgsToPlayPlayableWithSourceAndTargetDataConverter', {}),
        record(30, 'PlayPlayableWithSourceAndTargetHandler', dict(mConverter=dict(rid=40))),
        record(20, 'ECStatusInvokedEventSourceView', dict(mHandlers=[dict(rid=30)])),
        record(10, 'ECFightStatusPlayableView', dict(mPlayablePrefab=dict(m_FileID=0, m_PathID=0))),
    ]))


class RegistryTests(unittest.TestCase):
    def test_nested_handler_graph_preserves_both_serialized_orders(self):
        original = fixture()
        before = copy.deepcopy(original)
        result = CurrentBattleEntityViews.validate_registry(original)
        self.assertEqual([40, 30, 20, 10], [row['rid'] for row in result])
        self.assertEqual(10, CurrentBattleEntityViews.primary_record(result)['rid'])
        self.assertEqual(before, original)

    def test_existing_single_view_remains_valid(self):
        original = fixture()
        original['mECViews'] = [dict(rid=10)]
        original['references']['RefIds'] = [original['references']['RefIds'][-1]]
        self.assertEqual(1, len(CurrentBattleEntityViews.validate_registry(original)))

    def test_rejects_incomplete_or_unreviewed_registries(self):
        mutations = {
            'version': lambda d: d['references'].update(version=1),
            'duplicate record': lambda d: d['references']['RefIds'].append(copy.deepcopy(d['references']['RefIds'][0])),
            'duplicate registration': lambda d: d['mECViews'].append(dict(rid=10)),
            'missing nested record': lambda d: d['references']['RefIds'][1]['data'].update(mConverter=dict(rid=999)),
            'unknown type': lambda d: d['references']['RefIds'][0]['type'].update({'class': 'UnreviewedConverter'}),
            'wrong assembly': lambda d: d['references']['RefIds'][0]['type'].update(asm='UnreviewedAssembly'),
            'wrong namespace': lambda d: d['references']['RefIds'][0]['type'].update(ns='UnreviewedNamespace'),
            'handler registered as view': lambda d: d['mECViews'].append(dict(rid=30)),
            'missing primary registration': lambda d: d['mECViews'].pop(0),
            'unreferenced record': lambda d: d['references']['RefIds'].append(record(50,
                'StatusInvokedArgsToPlayPlayableWithSourceAndTargetDataConverter', {})),
            'two primary views': lambda d: (d['mECViews'].append(dict(rid=50)),
                d['references']['RefIds'].append(record(50, 'ECFightStatusPlayableView', {}))),
        }
        for name, mutate in mutations.items():
            with self.subTest(name=name):
                original = fixture()
                mutate(original)
                with self.assertRaises(ValueError):
                    CurrentBattleEntityViews.validate_registry(original)


if __name__ == '__main__':
    unittest.main()
