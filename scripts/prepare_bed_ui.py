"""Extract the original blessing rules popup and its embedded preview row."""
import copy
import hashlib
import json

from prepare_official_panels import PanelExporter, unique_sibling_paths
from prepare_official_city import ART, REFERENCE, ROOT
from official_resource_manifest import write_resources


def build():
    exporter = PanelExporter()
    popup = exporter.ui('prefabs_ui_furniture', '/BlessingLevelRuleDialog.prefab')
    unique_sibling_paths(popup['root'])

    def find(node, name):
        if node['name'] == name:
            return node
        for child in node['children']:
            result = find(child, name)
            if result is not None:
                return result

    row = copy.deepcopy(find(popup['root'], 'RuleFormDataItem'))
    row['active'] = True
    row['anchor_min'] = row['anchor_max'] = [0, 1]
    row['anchored_position'] = [0, 0]
    row['pivot'] = [0, 1]
    screens = json.loads((ART / 'ui.json').read_text(encoding='utf8'))
    screens['bed_rules'] = popup
    screens['bed_rule_row'] = {'source': dict(popup['source'], embedded_path=row['path']), 'root': row}
    exporter.save('ui.json', screens)
    manifest = json.loads((REFERENCE / 'source-manifest.json').read_text(encoding='utf8'))
    manifest['resources'].update(exporter.records)
    manifest['panel_prefabs'].update({key: screens[key]['source'] for key in ['bed_rules', 'bed_rule_row']})
    manifest['data_sha256']['ui.json'] = hashlib.sha256((ART / 'ui.json').read_bytes()).hexdigest()
    (REFERENCE / 'source-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf8')
    reference = ROOT / 'assets/reference/bed-ui'
    reference.mkdir(parents=True, exist_ok=True)
    record = {'version': '854_156383', 'popup': popup['source'], 'row': screens['bed_rule_row']['source'],
              'resources': exporter.records,
              'logic': {'foldouts': 'FurnitureBedDialog.OnLevelFoldoutBtn and OnFoldoutFoldoutBtn are independent',
                        'rules': 'BlessingRuleBtn opens BlessingLevelRuleDialog with 11 levels around the current blessing',
                        'progress': 'FurnitureBedDialog.HangUpTimer sets fillAmount = sumCount / maxCount',
                        'preview': 'BlessingLevelRuleItem uses base hourly output and HomeLevelInfo.FurnitureLevelUpLimit'},
              'adaptation': 'Rules text describes local behavior; modifier rows display the actual local aggregate properties without inventing source attribution'}
    (reference / 'source-manifest.json').write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding='utf8')
    write_resources(manifest)
    print(f'Bed rules: 2 original prefab entries, {len(exporter.records)} sprite source records')


if __name__ == '__main__':
    build()
