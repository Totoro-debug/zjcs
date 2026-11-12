"""Recover the seven supported mainline reward sprites with native geometry."""
import json
from pathlib import Path

import yaml
from official_apk import ROOT
from UnityPy.enums import ClassIDType
from official_manifest import read_asset_manifest
from prepare_home_bottom_bar_native import HomeBottomBarBinding
from prepare_partner_card_native import Dumper, sha
from validation_workspace import validation_directory


class HomeTaskRewardImages(HomeBottomBarBinding):
    def build(self):
        proof_path = ROOT / 'assets/reference/home-mainline-task-data-source-manifest.json'
        proof = json.loads(proof_path.read_bytes())
        if proof['manifest'] != self.recovery.apk.version:
            raise ValueError('Task reward image version differs')
        self.inputs[proof_path.relative_to(ROOT).as_posix()] = sha(proof_path.read_bytes())
        for relative, expected in proof['outputs'].items():
            path = ROOT / relative
            if sha(path.read_bytes()) != expected:
                raise ValueError('Selected task data differs: ' + relative)
            self.inputs[relative] = expected
        data = json.loads((ROOT / 'unity/Assets/Game/Resources/Home/home-mainline-task-data.json').read_bytes())
        records = read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive))
        items = {}
        for row in data['items']:
            address = 'Assets/Image/' + row['RawIcon']
            record, = [value for value in records if value.get('address', '').casefold() == address.casefold()
                or value['path'].casefold() == (address + '.png').casefold()]
            self.bind_dynamic_sprite(record['guid'])
            items[str(row['ClassId'])] = dict(item=row, **self.dynamic_sprites_by_guid[record['guid']])

        # Same native dependency fix-up used by PartnerGiftBinding.build. The
        # mutable queue includes newly discovered texture/material dependencies.
        for _, _, payload in self.queue:
            if isinstance(payload, dict):
                resolved = self.replace(payload)
                payload.clear()
                payload.update(resolved)
        outputs = {}
        folder = self.work / 'task-reward-images'
        for target, key, payload in self.queue:
            path = folder / target
            if isinstance(payload, dict):
                text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
                for file_id, content in payload.items():
                    kind, = content
                    text += '--- !u!' + str(int(ClassIDType[kind])) + ' &' + str(file_id) + '\n'
                    text += yaml.dump(content, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
                raw = text.encode('utf8')
            else:
                raw = payload
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
            meta = Path(str(path) + '.meta')
            meta.write_text('fileFormatVersion: 2\nguid: ' + key
                + '\nNativeFormatImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
            for value in (path, meta):
                outputs[value.relative_to(folder).as_posix()] = sha(value.read_bytes())
        for name in ('prepare_home_bottom_bar_native.py', 'prepare_partner_card_native.py'):
            path = ROOT / 'scripts' / name
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        receipt = dict(manifest=self.recovery.apk.version,
            generator_sha256=sha(Path(__file__).read_bytes()), inputs=self.inputs,
            items=items, objects=self.proof, outputs=outputs,
            scope='Only the seven current mainline reward sprites and original Windows texture dependencies; '
                'original mesh/index data checked by the shared native binder. ItemIcon prefab/config reuse and runtime binding remain required.')
        (self.work / 'home-task-reward-images-source-manifest.json').write_text(
            json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Original task reward images:', len(items), 'items;', len(outputs), 'outputs', flush=True)


if __name__ == '__main__':
    HomeTaskRewardImages(validation_directory()).build()
