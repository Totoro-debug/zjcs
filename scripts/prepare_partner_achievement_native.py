"""Recover the exact achievement subtree referenced by the original NPC detail."""
import copy
import json
from pathlib import Path
import re

from official_apk import ROOT
from prepare_partner_card_native import guid, script_id, sha
from prepare_partner_profile_native import PartnerProfileBinding
from validation_workspace import validation_directory


class PartnerAchievementBinding(PartnerProfileBinding):
    def register_scripts(self):
        super().register_scripts()
        folder = ROOT / 'unity/Assets/Game/Runtime/Official/PartnersUI/Roster/Achievements'
        for name in ('AchievementPanel', 'AchievementPanelBase', 'AchievementPanelItem'):
            path = folder / ('Partner' + name + '.cs')
            meta = Path(str(path) + '.meta')
            ref = dict(fileID=11500000, guid=guid(meta), type=3)
            self.scripts[name] = ref
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=ref,
                sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))
            self.class_ids[script_id('', name)] = ref
            self.script_guids.add(ref['guid'])
        official = ROOT / 'unity/Assets/Game/Runtime/Official'
        paths = {'SafeArea': official / 'WorldUI/WorldSafeArea.cs',
            'MirrorImage': official / 'WorldUI/MirrorImage.cs',
            'TopBarHandler': official / 'ProfessionUI/TopBarHandler.cs',
            'PanelTitle': official / 'ProfessionUI/PanelTitle.cs',
            'MaintainGlobalScaleUI': official / 'World/Map13MiniMapWindows/MaintainGlobalScaleUI.cs',
            'LayoutElementClamper': official / 'WorldUI/WorldLayoutElementClamper.cs',
            'UGUIJszx.HorizontalLayoutGroupJSZX': official / 'WorldUI/HorizontalLayoutGroupJSZX.cs'}
        for name, path in paths.items():
            meta = Path(str(path) + '.meta')
            ref = dict(fileID=11500000, guid=guid(meta), type=3)
            self.scripts[name] = ref
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=ref,
                sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))
            namespace, _, leaf = name.rpartition('.')
            self.class_ids[script_id(namespace, leaf or name)] = ref
            self.script_guids.add(ref['guid'])

    def friendship_templates(self):
        self.recovery.recover('NpcDetailPanel', self.work / 'partner-native')
        self.recovery.finalize()
        native, _, detail, record = self.recovery.outputs[0]
        consumer, = [row for row in detail['scripts'] if row['type'] == 'NpcDetailPanel']
        reference = detail['documents'][consumer['nativeFileID']]['MonoBehaviour']['mAchievementPanel']['fileID']
        root = detail['documents'][reference]['MonoBehaviour']['m_GameObject']['fileID']
        selected = set()

        def visit(go):
            selected.add(go)
            components = detail['documents'][go]['GameObject']['m_Component']
            selected.update(row['component']['fileID'] for row in components)
            transform = next(iter(detail['documents'][components[0]['component']['fileID']].values()))
            for child in transform['m_Children']:
                visit(next(iter(detail['documents'][child['fileID']].values()))['m_GameObject']['fileID'])

        visit(root)
        panel = copy.deepcopy(detail)
        panel['root'] = root
        panel['documents'] = {key: value for key, value in panel['documents'].items() if key in selected}
        panel['scripts'] = [row for row in panel['scripts'] if row['nativeFileID'] in selected]
        panel['objectMap'] = {key: value for key, value in panel['objectMap'].items() if value in selected}
        transform = next(iter(panel['documents'][panel['documents'][root]['GameObject']['m_Component'][0]['component']['fileID']].values()))
        old_parent = transform['m_Father']; transform['m_Father'] = dict(fileID=0)

        def check(value):
            if isinstance(value, dict):
                if set(value) == {'fileID'} and value['fileID'] and value['fileID'] not in selected:
                    raise ValueError('Achievement reference leaves original subtree: ' + repr(value))
                for entry in value.values(): check(entry)
            elif isinstance(value, list):
                for entry in value: check(entry)

        check(panel['documents'])
        self.template_selection = dict(consumer=consumer['original'], field='mAchievementPanel', original_parent=old_parent,
            root=root, mapped_objects=len(selected), adaptation='Detach only the parent of the exact referenced achievement subtree.')
        self.inputs[record['nativeSource']] = sha(native.read_bytes())
        self.inputs[record['nativeSource'] + '.meta'] = sha(Path(str(native) + '.meta').read_bytes())
        cache = self.recovery.proof['component_cache']; self.inputs[cache['path']] = cache['sha256']
        return [('NpcDetailAchievementContent', panel)]


def main():
    work = validation_directory()
    PartnerAchievementBinding(work, ROOT / 'unity/Assets/Game/Runtime/Official/PartnersUI/Roster/Profile').build()
    path = work / 'gift-assets/NpcDetailAchievementContent.prefab'
    raw = path.read_bytes()
    # Empty language keys are serialized strings, not YAML null values.
    raw = raw.replace(b'  myKey: null\n', b"  myKey: ''\n")
    path.write_bytes(raw)
    receipt = json.loads((work / 'partner-gift-native-source-manifest.json').read_bytes())
    receipt['outputs'][path.name] = sha(raw)
    receipt['generator_sha256'] = sha(Path(__file__).read_bytes())
    (work / 'partner-achievement-native-source-manifest.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Recovered exact original achievement region:', len(raw), 'bytes')


if __name__ == '__main__': main()
