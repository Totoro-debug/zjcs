"""Stage the original NPC panel controller with explicit local window services."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil

ROOT = Path(__file__).resolve().parents[1]
OUTPUT_ROOT = ROOT / 'unity/Assets/Game/Runtime/Official/PartnersUI/Roster'
PROOF = ROOT / 'assets/reference/partner-panel-controller-source-manifest.json'


def metadata(name):
    path = OUTPUT_ROOT / (name + '.meta')
    identity = path.relative_to(ROOT).as_posix()
    return ('fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs:partner-panel:' + identity).encode()).hexdigest()
            + '\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n'
            + '  executionOrder: 0\n  icon: {instanceID: 0}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode()


def build(work):
    work = work.resolve()
    if work.parent != ROOT / 'output/validation' or not (work / '.zjcs-validation.json').is_file():
        raise ValueError('An owned validation workspace is required')
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    inputs, outputs = {}, {}

    def source(name):
        path = ROOT / '.tools/source-packages/official-code' / name
        raw = path.read_bytes()
        if raw != (ROOT / '.tools/source-packages/current' / version / 'official-code' / name).read_bytes():
            raise ValueError('Panel source differs from selected version: ' + name)
        inputs[path.relative_to(ROOT).as_posix()] = hashlib.sha256(raw).hexdigest()
        return raw.decode('utf-8-sig').replace('\r\n', '\n')

    target = work / 'panel-controller'
    target.mkdir(exist_ok=True)

    def write(name, body):
        path = target / (name + '.cs')
        path.write_text('\n'.join(row.rstrip() for row in body.split('\n')), encoding='utf8')
        outputs[path.name] = hashlib.sha256(path.read_bytes()).hexdigest()

    enum = source('client-source/NpcPanelTabType.cs')
    write('NpcPanelTabType', 'namespace Zjcs.Official.PartnersUI\n{\n' + enum + '\n}\n')
    original_utils = source('client-source/NpcPanelUtils.cs')
    function_enum = source('common-source/Common/FunctionType.cs')
    mapping = re.findall(r'\[NpcPanelTabType\.(\w+)\] = FunctionType\.(\w+)', original_utils)
    if len(mapping) != 3:
        raise ValueError('The original NPC panel tab mapping changed')
    values = []
    for tab, function in mapping:
        identity, = re.findall(r'\b' + re.escape(function) + r'\s*=\s*(\d+)', function_enum)
        values.append('[NpcPanelTabType.' + tab + '] = ' + identity)
    write('PartnerPanelServices', '''using System.Collections.Generic;
using UnityEngine;
namespace Zjcs.Official.PartnersUI
{
    public interface IPartnerPanelServices
    {
        bool IsFunctionLimit(int functionType);
        void InitDialogueList(GameObject owner);
    }
    internal static class NpcPanelTabs
    {
        public static readonly IReadOnlyDictionary<NpcPanelTabType, int> Tab2FunctionTypeDict =
            new Dictionary<NpcPanelTabType, int> { ''' + ', '.join(values) + ''' };
    }
}
''')
    text = source('client-source/NpcPanel.cs')
    text = re.sub(r'^using (Common|UnityEngine.UI.Ext);\n', '', text, flags=re.M)
    text = re.sub(r'^\[UIWindow[^\n]*\n', '', text, flags=re.M)
    text = text.replace('public class NpcPanel : MonoBehaviour\n{', '''public class NpcPanel : MonoBehaviour
{
    private IPartnerPanelServices services;
    public void Configure(IPartnerPanelServices value)
    {
        services = value ?? throw new System.ArgumentNullException(nameof(value));
    }
''')
    text = text.replace('Player mainPlayer = MeManager.MainPlayer;', 'IPartnerPanelServices mainPlayer = services;')
    text = text.replace('NpcPanelUtils.Tab2FunctionTypeDict', 'NpcPanelTabs.Tab2FunctionTypeDict')
    text = text.replace('mNpcDialogueListGo.GetComponent<NpcPanelDialogueList>().Init();', 'services.InitDialogueList(mNpcDialogueListGo);')
    text = re.sub(r'\bFunctionType\b', 'int', text)
    text = text.replace('item.Deconstruct(ref npcPanelTabType2, ref tabItem);', 'npcPanelTabType2 = item.Key; tabItem = item.Value;')
    text = text.replace('item.Deconstruct(ref npcPanelTabType, ref functionType);', 'npcPanelTabType = item.Key; functionType = item.Value;')
    text = text.replace('item.Deconstruct(ref npcPanelTabType, ref tabItem);', 'npcPanelTabType = item.Key; tabItem = item.Value;')
    text = re.sub(r'\bTabGroup\b', 'WorldTabGroup', text)
    text = re.sub(r'\bTabItem\b', 'WorldTabItem', text)
    text = text.replace('StateSwitcher<', 'WorldStateSwitcher<')
    text = 'using Object = UnityEngine.Object;\nusing Zjcs.Official.ProfessionUI;\nusing Zjcs.Official.WorldUI;\n' + text
    write('NpcPanel', '// Original panel branches; see panel-controller-source-manifest.json.\nnamespace Zjcs.Official.PartnersUI\n{\n' + text + '\n}\n')
    receipt = dict(manifest=version, inputs=inputs, outputs=outputs,
                   output_root=OUTPUT_ROOT.relative_to(ROOT).as_posix(),
                   metadata={name + '.meta': hashlib.sha256(metadata(name)).hexdigest() for name in outputs},
                   generator_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
                   adaptations=['Instance service ports replace global player and dialogue-list lookup.',
                                'Existing official tabs, title, dictionary and state-switcher implementations are reused.',
                                'All original tab gates, callback order, fallback and disable reset branches remain.'])
    (work / 'panel-controller-source-manifest.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    return target


def install(work):
    receipt = json.loads((work / 'panel-controller-source-manifest.json').read_bytes())
    previous = json.loads(PROOF.read_bytes()) if PROOF.is_file() else {}
    for name in receipt['outputs']:
        target = OUTPUT_ROOT / name
        if target.exists() and hashlib.sha256(target.read_bytes()).hexdigest() != previous.get('outputs', {}).get(name):
            raise ValueError('Panel source differs from prior provenance: ' + name)
        meta = OUTPUT_ROOT / (name + '.meta')
        if meta.exists() and meta.read_bytes().replace(b'\r\n', b'\n') != metadata(name):
            raise ValueError('Panel metadata differs: ' + name)
    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    for name in receipt['outputs']:
        shutil.copy2(work / 'panel-controller' / name, OUTPUT_ROOT / name)
        (OUTPUT_ROOT / (name + '.meta')).write_bytes(metadata(name))
    shutil.copy2(work / 'panel-controller-source-manifest.json', PROOF)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--workspace', required=True, type=Path)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    print(build(args.workspace))
    if args.install:
        install(args.workspace.resolve())
