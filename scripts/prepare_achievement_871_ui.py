"""Convert the approved achievement and score reward prefabs from 871."""
from __future__ import annotations

import hashlib
import json

from official_apk import ROOT
from prepare_official_panels import PanelExporter, unique_sibling_paths


OUTPUT = ROOT / 'assets/official/achievements/ui.json'
RECEIPT = ROOT / 'assets/reference/achievements/achievement-ui-source-manifest.json'
PREFABS = (
    ('achievement', 'defaultpackage_assets_prefabs_ui_achievement.bundle', '/AchievementPanel.prefab'),
    ('groups', 'defaultpackage_assets_prefabs_ui_achievement.bundle', '/AchievementGroupPanel.prefab'),
    ('achievement_item', 'defaultpackage_assets_prefabs_ui_achievement.bundle', '/AchievementPanelItem.prefab'),
    ('group_content', 'defaultpackage_assets_prefabs_ui_achievement.bundle', '/AchievementGroupContent.prefab'),
    ('score', 'defaultpackage_assets_prefabs_ui_gachapanel.bundle', '/ScoreRewardUI.prefab'),
    ('score_dialog', 'defaultpackage_assets_prefabs_ui_gachapanel.bundle', '/ScoreRewardDialog.prefab'),
    ('score_item', 'defaultpackage_assets_prefabs_ui_gachapanel.bundle', '/ScoreRewardDialogItem.prefab'),
)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path, value, *, indent=None):
    encoded = json.dumps(value, ensure_ascii=False, separators=None if indent else (',', ':'),
                         indent=indent, allow_nan=False)
    path.write_bytes((encoded + '\n').encode('utf-8'))


def nodes(root):
    yield root
    for child in root.get('children', []):
        yield from nodes(child)


def build():
    exporter = PanelExporter()
    panels = {}
    for key, bundle, path in PREFABS:
        panel = exporter.ui(bundle, path)
        if panel['source'].get('manifest') != '871_156598':
            raise ValueError('Achievement UI input is not from 871_156598: ' + path)
        unique_sibling_paths(panel['root'])
        panels[key] = panel

    for key, panel in panels.items():
        all_nodes = list(nodes(panel['root']))
        if not all_nodes or any(not node.get('path', '').startswith('/' + panel['root']['name']) for node in all_nodes):
            raise ValueError('Converted achievement hierarchy has an invalid root path: ' + key)
        if any(not isinstance(node.get('scripts'), list) for node in all_nodes):
            raise ValueError('Converted achievement hierarchy lost component identities: ' + key)

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    write_json(OUTPUT, panels)
    receipt = {
        'format': 1,
        'manifest': exporter.apk.version,
        'prefabs': {key: panel['source'] for key, panel in panels.items()},
        'inputs': exporter.inputs,
        'resources': exporter.records,
        'unsupported': exporter.unsupported,
        'outputs': {OUTPUT.relative_to(ROOT).as_posix(): sha(OUTPUT)},
        'operation': 'Convert the seven verified achievement and score reward prefabs, their UGUI/TMP hierarchy, original component fields, animation tracks and referenced Sprites from the selected 871 resources.',
    }
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    write_json(RECEIPT, receipt, indent=2)
    print('871 achievement UI:', len(panels), 'prefabs,', sum(len(list(nodes(x['root']))) for x in panels.values()),
          'nodes,', len(exporter.records), 'sprites,', len(exporter.inputs), 'inputs')
    return receipt


if __name__ == '__main__':
    build()
