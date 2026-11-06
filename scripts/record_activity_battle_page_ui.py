"""Record hand-adapted original activity-page controllers and field identities."""
import hashlib
import json
import re
from pathlib import Path

from official_apk import ROOT
from prepare_partner_input_dialog import metadata


def main():
    client = ROOT / '.tools/source-packages/current/884_156692/official-code/client-source'
    target = ROOT / 'unity/Assets/Game/Runtime/Official/ActivityBattleUI'
    sources, outputs, fields = {}, {}, {}
    for name in ('ActivityDomainBossPanel', 'ActivityDomainBossPanelDetailContent', 'ActivityDomainBossPanelBattleContent',
                 'NavigateBtn', 'SecretAreaBattleButtons'):
        source, port = client / (name + '.cs'), target / (name + '.cs')
        text = source.read_text('utf8')
        names = re.findall(r'\[SerializeField\]\s*private [^;\n]+ (m\w+);', text)
        port_text = port.read_text('utf8')
        if any(not re.search(r'\b' + field + r'\b', port_text) for field in names):
            raise ValueError('Original serialized activity field omitted: ' + name)
        fields[name] = names
        sources[source.relative_to(ROOT).as_posix()] = hashlib.sha256(source.read_bytes()).hexdigest()
        for path in (port, metadata(port)):
            outputs[path.relative_to(ROOT).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
    for name in ('ActivityDomainBossPageBindings.cs', 'Zjcs.Official.ActivityBattleUI.asmdef'):
        path = target / name
        for output in (path, metadata(path)):
            outputs[output.relative_to(ROOT).as_posix()] = hashlib.sha256(output.read_bytes()).hexdigest()
    receipt = dict(manifest='884_156692', sources=sources, outputs=outputs, serialized_fields=fields,
        generator_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        adaptation='Retain original tab order, selected difficulty, selector callbacks, red-dot scans, layer states, '
                    'challenge count and reward icon/attachment operations. Replace online data managers/requests '
                    'with an explicit local snapshot and command adapter; independent checkpoint HP is user-approved. '
                    'The local period opens on first entry; no server summon preview is fabricated. '
                    'Full native page binding, local adapter and Play Mode verification pending.')
    (ROOT / 'assets/reference/daily-challenges/activity-page-ui-source-manifest.json').write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')


if __name__ == '__main__': main()
