"""Record the local star-god state ports and their selected source receipts."""
import hashlib
import json
from pathlib import Path

from official_apk import ROOT


def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    receipts = ['partner-condition-source-manifest.json', 'partner-star-god-ui-source-manifest.json',
                'partner-star-god-native-source-manifest.json']
    sources = {}
    for name in receipts:
        path = ROOT / 'assets/reference' / name
        receipt = json.loads(path.read_bytes())
        if receipt['manifest'] != '884_156692': raise ValueError('Selected star-god source version differs')
        for key in ('installed_outputs', 'installed_output_sha256'):
            for relative, expected in receipt.get(key, {}).items():
                if digest(ROOT / relative) != expected: raise ValueError('Installed source changed: ' + relative)
        sources[path.relative_to(ROOT).as_posix()] = digest(path)
    native = json.loads((ROOT / 'assets/reference' / receipts[-1]).read_bytes())
    if native['star_god_images']['meet_item_ids'] != [1357, 1358, 1359, 1367, 1368, 1369, 1376, 1377, 1378]:
        raise ValueError('Selected original star-god fragment closure differs')
    runtime = ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI'
    paths = [runtime / (name + suffix) for name in ('OfflinePartnerStarGodUiServices', 'PartnerStarGodPanelView')
             for suffix in ('.cs', '.cs.meta')]
    paths += [ROOT / 'scripts' / name for name in ('prepare_partner_star_god_local_validation.py',
        'prepare_partner_star_god_validation.py', 'fixtures/partner_star_god_local_validation.cs')]
    output = dict(manifest='884_156692', generator_sha256=digest(Path(__file__)),
        source_receipt_sha256=sources, binding_sha256={path.relative_to(ROOT).as_posix(): digest(path) for path in paths},
        adaptations=['Use existing local roster transactions and notify only after saving.',
            'Preserve owned NPC identity and update values before forwarding original page events.',
            'Preview records remain transient; original GetMeetNpcItems preserves direct-child order.',
            'Bind original native page and pool before activation; restore image/localization providers when closing.',
            'Use actual selected sprites and original ItemIcon context; no new UI layout or replacement art.'])
    (ROOT / 'assets/reference/partner-star-god-local-source-manifest.json').write_text(
        json.dumps(output, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Recorded selected star-god state/view bindings', len(paths), flush=True)


if __name__ == '__main__': main()
