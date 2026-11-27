"""Record the shared original use-count slider and acceleration compatibility adapter."""
import argparse
import hashlib
import json
from pathlib import Path
from official_source import OfficialSource
from prepare_partner_input_dialog import metadata

ROOT = Path(__file__).resolve().parents[1]
RECEIPT = ROOT / 'assets/reference/shared-use-count-source-manifest.json'


def build(check=False):
    code = ROOT / '.tools/source-packages/current' / OfficialSource().version / 'official-code'
    runtime = ROOT / 'unity/Assets/Game/Runtime/Official'
    common = runtime / 'HomeCountControlsUI/SetUseCountSlider.cs'
    if not check: metadata(common)
    paths = [Path(__file__), code / 'client-source/SetUseCountSlider.cs', code / 'client-source/SetCountSlider.cs',
        code / 'common-source/Common/MathUtils.cs',
        ROOT / 'scripts/fixtures/item_use_count_validation.cs', ROOT / 'scripts/test_item_use_count.ps1',
        ROOT / 'scripts/fixtures/home_fast_hangup_native_validation.cs',
        ROOT / 'assets/reference/home-fast-hangup-native-source-manifest.json']
    paths += [runtime / relative for relative in ('HomeCountControlsUI/SetUseCountSlider.cs',
        'HomeCountControlsUI/SetUseCountSlider.cs.meta', 'HomeCountControlsUI/SetCountSlider.cs',
        'HomeCountControlsUI/IHomeCountColors.cs', 'HomeCountControlsUI/Zjcs.Official.HomeCountControlsUI.asmdef',
        'HomeFastHangUpUI/SetUseCountSlider.cs', 'HomeFastHangUpUI/SetUseCountSlider.cs.meta',
        'HomeFastHangUpUI/HomeFastHangUpServices.cs', 'HomeFastHangUpUI/Zjcs.Official.HomeFastHangUpUI.asmdef')]
    sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
    proof = dict(manifest=OfficialSource().version, inputs={path.relative_to(ROOT).as_posix(): sha(path) for path in paths},
        scope='Original use-count clamping, slider/button updates and message cooldown shared by item use and acceleration. Existing acceleration native script identity and services preserved. Complete item-use windows validated separately.')
    if check:
        if json.loads(RECEIPT.read_bytes()) != proof: raise ValueError('Shared use-count source boundary differs')
        print('Shared original use-count source and acceleration adapter verified')
    else: RECEIPT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
