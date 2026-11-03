"""Stage the original detail-talk region in the owned imported Play Mode cache."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
from official_apk import ROOT
from validation_workspace import require_temporary_unity_project


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--project', required=True, type=Path)
    project = require_temporary_unity_project(parser.parse_args().project)
    proof = json.loads((ROOT / 'assets/reference/partner-detail-talk-source-manifest.json').read_bytes())
    for relative, expected in proof['installed_output_sha256'].items():
        source = ROOT / relative
        if hashlib.sha256(source.read_bytes()).hexdigest() != expected: raise ValueError('Detail talk installed output changed: ' + relative)
        target = project / source.relative_to(ROOT / 'unity')
        target.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(source, target)
    fixture = project / 'Assets/Editor/PartnerDetailTalkValidation.cs'
    shutil.copy2(ROOT / 'scripts/fixtures/partner_detail_talk_validation.cs', fixture)
    source = ROOT / 'unity/Assets/Game/Runtime/Official/StoryBubbles'
    target = project / source.relative_to(ROOT / 'unity')
    shutil.copytree(source, target, dirs_exist_ok=True)
    assembly, = (project / 'Assets/Editor').glob('*.asmdef')
    data = json.loads(assembly.read_bytes())
    for name in ('Zjcs.Official.PartnerDetailUI', 'Zjcs.Official.StoryBubbles'):
        if name not in data['references']: data['references'].append(name)
    assembly.write_text(json.dumps(data, indent=2) + '\n', 'utf8', newline='\n')
    host = project / 'Assets/Editor/PartnerPanelHostValidation.cs'
    text = host.read_text('utf8')
    anchor = '        while (achievementUi.MoveNext()) yield return achievementUi.Current;'
    addition = '\n        var detailTalk = PartnerDetailTalkValidation.Check(canvas.transform, cameraUi, font);\n        while (detailTalk.MoveNext()) yield return detailTalk.Current;'
    if text.count(anchor) != 1: raise ValueError('Expected current original achievement UI fixture')
    if addition not in text: host.write_text(text.replace(anchor, anchor + addition), 'utf8', newline='\n')
    print('Prepared original detail talk region/animation lifecycle Play Mode fixture')


if __name__ == '__main__': main()
