"""Convert the pinned ChangeAppearancePanel, serialized bindings and fee icon.

Requires the shared workspace conversion lock, like the other UI converters.
"""
import hashlib
import json

from prepare_official_panels import PanelExporter, unique_sibling_paths
from prepare_official_city import ART, REFERENCE, ROOT
from official_unity_schema import ScriptSchemas
from official_resource_manifest import write_resources
from npc_travel_language import language
from UnityPy.classes.PPtr import PPtr


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


class AppearanceExporter(PanelExporter):
    def __init__(self):
        super().__init__()
        self.schemas = ScriptSchemas()
        self.inputs = {}
        self.keys = set()

    def convert(self, value, owner):
        if isinstance(value, list):
            return [self.convert(v, owner) for v in value]
        if not isinstance(value, dict):
            return value
        if 'm_PathID' in value:
            if not value['m_PathID']:
                return None
            ptr = PPtr(**value, assetsfile=owner.assets_file)
            if ptr.type.name == 'Sprite':
                return self.sprite(ptr)
            obj = ptr.deref()
            if obj.type.name != 'GameObject':
                obj = obj.read(check_read=False).m_GameObject.deref()
            return self.object_path(PPtr(m_FileID=0, m_PathID=obj.path_id, assetsfile=obj.assets_file))
        return {k: str(v) if k == 'rid' else self.convert(v, owner) for k, v in value.items()}

    def ui_node(self, obj, path=''):
        node = super().ui_node(obj, path)
        if node is None:
            return None
        for entry in obj.read().m_Component:
            component = entry.component.deref()
            if component.type.name != 'MonoBehaviour':
                continue
            cls, raw, _ = self.mono(component)
            if cls not in {'ChangeAppearancePanel', 'CreatePlayerDIYContent', 'AppearancePreviewModel', 'ImageLocator',
                           'PlayerCostMoneyContent', 'MultiLanguageText'}:
                continue
            fields = self.schemas.read(component)
            self.inputs[component.assets_file.name + ':' + str(component.path_id)] = dict(
                self.apk.provenance(component), type=component.type.name, bytes=len(raw),
                sha256=hashlib.sha256(raw).hexdigest())
            if cls == 'MultiLanguageText':
                if fields['myKey']:
                    node['localization_key'] = fields['myKey']
                    self.keys.add(fields['myKey'])
            else:
                node.setdefault('original_bindings', {})[cls] = {
                    k: self.convert(v, component) for k, v in fields.items()
                    if k not in ['m_GameObject', 'm_Enabled', 'm_Script', 'm_Name']}
        return node


def build():
    e = AppearanceExporter()
    panel = e.ui('prefabs_ui_appendappearance', '/ChangeAppearancePanel.prefab')
    unique_sibling_paths(panel['root'])
    rules = json.loads((ART / 'rules.json').read_text('utf8'))
    settings = rules['game_settings']
    item = next(v for v in rules['item'] if int(v['ClassId']) == int(settings['ChangeAppearanceCostItemId']))
    texts, language_sources = language(e.keys | {
        'ui_change_appearance_refuse_tip_text', 'ui_change_appearance_conflict_confirm_tip_text',
        'ui_change_appearance_confirm_tip_text', 'ui_change_appearance_success_tip_text',
        'ui_change_appearance_close_tip_text', 'ui_common_item_not_enough', 'confirm', 'cancel', 'confirmation',
        'item_119_desc', 'item_119_func_desc', 'ItemType.Other'})
    required = {'ui_change_appearance_refuse_tip_text', 'ui_change_appearance_confirm_tip_text',
                'ui_change_appearance_success_tip_text', 'ui_change_appearance_close_tip_text',
                'ui_common_item_not_enough', 'confirm', 'cancel', 'confirmation'}
    if not required <= texts.keys():
        raise ValueError('Original appearance language keys missing: ' + str(required - texts.keys()))

    def localize(node):
        key = node.get('localization_key')
        if key in texts and 'text' in node:
            node['text']['value'] = texts[key]
        for child in node['children']:
            localize(child)
    localize(panel['root'])
    # ItemInfo.GetIconPath returns RawIcon for this non-appearance item.
    # Like prepare_gameplay_assets, resolve the Sprite subasset by address;
    # these subassets need not have a separate .png GUID in the manifest.
    prefix = 'defaultpackage_assets_image_' + item['RawIcon'].rsplit('/', 1)[0].replace('/', '_').lower()
    candidates = []
    for bundle in e.apk.index:
        if not bundle.startswith(prefix):
            continue
        for name, ptr in e.apk.container(bundle):
            if ptr.type.name == 'Sprite' and name.rsplit('/', 1)[-1].rsplit('.', 1)[0].lower() == item['RawIcon'].rsplit('/', 1)[-1].lower():
                candidates.append(ptr)
    if len(candidates) != 1:
        raise ValueError('Original appearance fee icon is not unique: ' + item['RawIcon'])
    icon = e.sprite(candidates[0])
    data = dict(panel=panel, texts=texts, fee=dict(item_id=int(settings['ChangeAppearanceCostItemId']),
                count=int(settings['ChangeAppearanceCostItemCount']), icon=icon))
    e.save('appearance-ui.json', data)
    manifest = json.loads((REFERENCE / 'source-manifest.json').read_text('utf8'))
    manifest['resources'].update(e.records)
    manifest['data_sha256']['appearance-ui.json'] = sha(ART / 'appearance-ui.json')
    manifest.setdefault('explicit_data_resources', []).append('appearance-ui.json')
    manifest['explicit_data_resources'] = sorted(set(manifest['explicit_data_resources']))
    (REFERENCE / 'source-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), 'utf8')
    write_resources(manifest)
    ref = ROOT / 'assets/reference/character-genders/appearance-source-manifest.json'
    sources = ['client-source/ChangeAppearancePanel.cs', 'client-source/PlayerCostMoneyContent.cs',
               'common-source/PlayerServiceProxyImpl.cs', 'client-source/AppearanceSetContext.cs',
               'client-source/ImageLocator.cs', 'client-source/MoneyUtils.cs', 'client-source/ItemDetailDialog.cs']
    source_root = ROOT / '.tools/source-packages/official-code'
    receipt = dict(manifest='854_156383', prefab=panel['source'], inputs=e.inputs, resources=e.records,
        language_sources=language_sources, logic_inputs={str((source_root/n).relative_to(ROOT).as_posix()): sha(source_root/n) for n in sources},
        reused_sources={'assets/official/ui.json':sha(ART/'ui.json'),'assets/official/icons.json':sha(ART/'icons.json')},
        rules_sha256=sha(ART/'rules.json'), outputs={'assets/official/appearance-ui.json': sha(ART/'appearance-ui.json')})
    ref.parent.mkdir(parents=True, exist_ok=True)
    ref.write_text(json.dumps(receipt, ensure_ascii=False, indent=2), 'utf8')
    print('Original ChangeAppearancePanel converted with fee', data['fee']['item_id'], data['fee']['count'])


if __name__ == '__main__':
    build()
