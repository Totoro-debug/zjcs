"""Add the existing player-info branch fixture to the owned native partner project."""
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    project = work / 'star-god-unity'
    if not (work / '.zjcs-validation.json').is_file() or not (work / 'npc-icon-validation-inputs.json').is_file():
        raise ValueError('Prepare an owned populated native partner window first')
    copied = {}
    for name in ('ProfessionPlayerInfoValidation.cs', 'ProfessionValidationWorkspace.cs'):
        source = ROOT / 'unity/Assets/Game/Editor/ProfessionUI' / name
        text = source.read_text('utf8')
        if name == 'ProfessionValidationWorkspace.cs':
            before = 'Path.Combine(workspace, "unity", "Assets")'
            if text.count(before) != 1:
                raise ValueError('Review isolated project boundary')
            text = text.replace(before, 'Path.Combine(workspace, "star-god-unity", "Assets")')
        target = project / 'Assets/Editor' / name
        if target.exists() and target.read_text('utf8') != text:
            raise ValueError('Preserve modified player-info fixture: ' + str(target))
        target.write_text(text, 'utf8', newline='\n')
        copied[source.relative_to(ROOT).as_posix()] = dict(source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
            isolated_sha256=hashlib.sha256(target.read_bytes()).hexdigest())
    (work / 'player-info-validation-inputs.json').write_text(json.dumps(copied, indent=2) + '\n', 'utf8', newline='\n')
    print('Prepared existing player-info branch/pointer fixture; only the isolated project folder check is adapted')


if __name__ == '__main__':
    main()
