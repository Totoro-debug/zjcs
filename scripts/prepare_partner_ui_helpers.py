"""Migrate the selected official partner filter/sort/card presentation helpers.

Only namespaces, existing shared UI type names and the offline instantiate/localize
boundary change. Runtime branches, serialized names and callback order stay original.
"""
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/PartnersUI'
CLIENT = '''BaseSortGroup BaseSortGroupItem CollectSortGroup CollectSortGroupItem CollectSortType
BaseFilterGroup CommonFilterGroup BaseFilterGroupItem CommonFilterType
FilterGroupForProfession FilterGroupItemForProfession FilterGroupForItemType FilterGroupItemForItemType
FilterGroupForQuality FilterGroupItemForRank FilterGroupForElement FilterGroupItemForElement
FilterGroupForProperty FilterGroupItemForProperty BaseSwitchGroup BaseSwitchGroupItem CommonSwitchType
SwitchShowBtn MultiTextsControler FilterSortPanel ListItemShowSequence IShowSequenceItem NpcMeetProgressItem'''.split()
COMMON = 'Profession ItemType Quality ElementType PropFilterType'.split()
NAMESPACE = 'Zjcs.Official.PartnersUI'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def meta(path, folder=False):
    target = Path(str(path) + '.meta')
    if target.exists():
        return
    guid = hashlib.md5(('zjcs-partner-ui:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
    importer = 'DefaultImporter' if folder else 'MonoImporter' if path.suffix == '.cs' else 'AssemblyDefinitionImporter'
    target.write_text('fileFormatVersion: 2\nguid: ' + guid + '\n' + ('folderAsset: yes\n' if folder else '') +
        importer + ':\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n', 'utf8')


def main():
    selection = json.loads((ROOT / 'official-source.json').read_text('utf8'))
    version = selection['active']
    inputs, outputs = {}, {}

    def source(name):
        path = ROOT / '.tools/source-packages/official-code' / name
        archived = ROOT / '.tools/source-packages/current' / version / 'official-code' / name
        raw = path.read_bytes()
        if raw != archived.read_bytes():
            raise ValueError('Selected official source differs: ' + name)
        inputs[path.relative_to(ROOT).as_posix()] = sha(raw)
        return raw.decode('utf-8-sig').replace('\r\n', '\n')

    def write(name, text):
        path = DEST / name
        path.write_text(text, 'utf8')
        meta(path)
        outputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())

    DEST.mkdir(parents=True, exist_ok=True)
    meta(DEST, folder=True)
    for name in CLIENT + COMMON:
        text = source(('client-source/' if name in CLIENT else 'common-source/Common/') + name + '.cs')
        text = text.replace('namespace Common;\n', '')
        text = re.sub(r'^using (Common|NaughtyAttributes|UnityEngine.UI.Ext);\n', '', text, flags=re.M)
        text = re.sub(r'^\s*\[SearchableEnum\]\n', '', text, flags=re.M)
        text = re.sub(r'^\s*//IL_[^\n]*\n', '', text, flags=re.M)
        text = re.sub(r'\bStateSwitcher<', 'Zjcs.Official.WorldUI.WorldStateSwitcher<', text)
        text = text.replace('UIUtils.Instantiate(gameObject, slot)', 'PartnerUiInstantiation.Instantiate(gameObject, slot)')
        aliases = ('using Object = UnityEngine.Object;\n'
                   'using TabGroup = Zjcs.Official.WorldUI.WorldTabGroup;\n'
                   'using TabItem = Zjcs.Official.WorldUI.WorldTabItem;\n'
                   'using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;\n'
                   'using LanguageUtils = Zjcs.Official.PartnersUI.PartnerUiLanguage;\n')
        write(name + '.cs', '// Migrated from selected official sources; see partner-ui-helpers-source-manifest.json.\n'
            + 'namespace ' + NAMESPACE + '\n{\n' + aliases + text + '\n}\n')

    language = source('common-source/Common/LanguageUtils.cs')
    methods = []
    for signature in ('GetEnumKey(object obj)', 'GetProfessionNameKey(Profession profession)'):
        pattern = r'\tpublic static string ' + re.escape(signature) + r'\n\t\{\n.*?\n\t\}'
        method, = re.findall(pattern, language, flags=re.S)
        methods.append(method)
    write('PartnerUiLanguage.cs', 'namespace ' + NAMESPACE + '\n{\n'
        '// Exact official LanguageUtils key builders. Localization is provided by WorldMultiLanguageText.\n'
        'internal static class PartnerUiLanguage\n{\n' + '\n\n'.join(methods) + '\n}\n}\n')
    source('client-source/UIUtils.cs')
    source('client-source/EUIInstantiateFlag.cs')
    write('PartnerUiInstantiation.cs', '''using UnityEngine;

namespace Zjcs.Official.PartnersUI
{
    internal static class PartnerUiInstantiation
    {
        // UIUtils.Instantiate(GameObject, parent, All): the partner filter clones
        // have no injected services. Keep all four original ResetUI operations.
        internal static GameObject Instantiate(GameObject prefab, Transform parent)
        {
            GameObject instance = Object.Instantiate(prefab, parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            instance.SetActive(true);
            return instance;
        }
    }
}
''')
    write('Zjcs.Official.PartnersUI.asmdef', json.dumps(dict(name=NAMESPACE, rootNamespace=NAMESPACE,
        references=['Unity.ugui', 'Zjcs.Official.WorldUI']), indent=2) + '\n')
    receipt = ROOT / 'assets/reference/partner-ui-helpers-source-manifest.json'
    receipt.write_text(json.dumps(dict(format=1, official_source=version, apk=selection['profiles'][version]['apk'],
        generator_sha256=sha(Path(__file__).read_bytes()),
        source_sha256=inputs, output_sha256=outputs,
        adaptations=['Namespace isolation; shared WorldUI text/tab/state components.',
            'Removed editor-only SearchableEnum attribute and decompiler IL comments.',
            'No service injection is needed by cloned filter components; retain original ResetUI(All) operations.'],
        scope='Filter/sort/switch groups, display sequence and meet progress helpers only; not an installed partner screen.'),
        ensure_ascii=False, indent=2) + '\n', 'utf8')
    print('Migrated', len(outputs), 'partner UI helper files from', len(inputs), 'verified inputs.')


if __name__ == '__main__':
    main()
