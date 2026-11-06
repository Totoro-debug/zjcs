"""Port the selected original NPC region selection and starting animation methods."""
import hashlib
import json
import re
from pathlib import Path

from official_apk import ROOT
from prepare_partner_gift_configuration import method
from prepare_partner_input_dialog import metadata


def main():
    inputs, methods = {}, {}
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    def read(relative):
        path = code / relative
        raw = path.read_bytes(); inputs[path.relative_to(ROOT).as_posix()] = hashlib.sha256(raw).hexdigest()
        return raw.decode('utf-8-sig').replace('\r\n', '\n')
    def selected(relative, signature):
        body = method(read(relative), signature)
        methods[relative + ':' + signature] = hashlib.sha256(body.encode()).hexdigest()
        return body
    enum = read('common-source/Common/ENpcFullBodyImageType.cs')
    if re.findall(r'^\s*(\w+),?$', enum, re.M)[-3:] != ['None', 'Static', 'Live']:
        raise ValueError('Review original full-body enum identity')
    region = read('launch-source/Common/RegionCodeEnum.cs')
    if not re.search(r'public enum RegionCodeEnum\n\{\n\tCN(?: = 0)?,', region):
        raise ValueError('Review selected CN region identity')
    read('common-source/Common/NpcRoleType.cs')
    source = 'common-source/Common/NpcInfo.cs'
    bodies = []
    for signature in (
            'public ENpcFullBodyImageType GetFullBodyImagePath(out string path, RegionCodeEnum? region = null)',
            'public bool TryGetFullBodyImagePath(ENpcFullBodyImageType imageType, out string path, RegionCodeEnum? region = null)',
            'private bool TryGetFullBodyImagePathInternal(ENpcFullBodyImageType imageType, RegionCodeEnum targetRegion, out string path)',
            'private bool TryGetRegionImagePath(Dictionary<RegionCodeEnum, string> regionImages, RegionCodeEnum targetRegion, out string path)'):
        body = selected(source, signature)
        body, count = re.subn(r'\t+GLogger(?:Warn|Error)InterpolatedStringHandler message =.*?\n\t+Log\.(?:Warn|Error)\([^\n]*\);',
            lambda match: '\t\tDebug.Log' + ('Warning' if 'GLoggerWarn' in match[0] else 'Error') +
                '("NpcInfo.' + ('GetFullBodyImagePath' if 'GetFullBodyImagePath(out' in signature else 'TryGetFullBodyImagePath') +
                ' npcId = " + Id + " ' + ('region = " + regionCodeEnum' if 'GLoggerWarn' in match[0] else '当前类型不支持! type = " + imageType') + ');',
            body, flags=re.S)
        if count != (0 if signature.startswith('private bool TryGetRegionImagePath') else 1):
            raise ValueError('Review original region diagnostic branches')
        body = body.replace('ENpcFullBodyImageType', 'PartnerFullBodyImageType').replace('RegionCodeEnum', 'int')
        body = body.replace('ServerInfoManager.Instance.RegionCode', '0')
        body = body.replace('NpcRegionImageInfo npcRegionImageInfo = Parsers.Instance.NpcRegionImageInfoParser.Get(Id);',
                            'RegionImages npcRegionImageInfo = getRegion(Id);')
        bodies.append(body)
    starting = selected('client-source/AppearanceSystem.Runtime/AppearanceUtils.cs', 'public static string GetNpcStartingAnimName(int npcId)')
    read('framework-firstpass-source/Framework.Device/DesktopDevice.cs')
    selected('framework-firstpass-source/Framework.Device/DeviceUtility.cs', 'public static MemorySizeLevel GetMemorySizeLevel()')
    template = ROOT / 'scripts/templates/partner_detail/OfflinePartnerDetailConfiguration.cs'
    inputs[template.relative_to(ROOT).as_posix()] = hashlib.sha256(template.read_bytes()).hexdigest()
    text = template.read_text('utf8').replace('/*REGION_METHODS*/', '\n\n'.join(bodies)).replace('/*STARTING_ANIMATION*/', starting)
    text = re.sub(r'(?m)^[ \t]+', lambda match: match[0].replace('\t', '    '), text)
    if any(token in text for token in ('Parsers.', 'ServerInfoManager', 'GLogger', '/*')):
        raise ValueError('Unbound original detail configuration')
    dest = ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerDetailConfiguration.cs'
    receipt = ROOT / 'assets/reference/partner-detail-configuration-source-manifest.json'
    previous = json.loads(receipt.read_bytes()) if receipt.exists() else {}
    generated = '// Selected official methods; see partner-detail-configuration-source-manifest.json.\n' + text
    if (dest.exists() and dest.read_text('utf8') != generated
            and previous.get('outputs', {}).get(dest.relative_to(ROOT).as_posix()) != hashlib.sha256(dest.read_bytes()).hexdigest()):
        raise ValueError('Preserve existing detail configuration change')
    dest.write_text(generated, 'utf8', newline='\n')
    outputs = {path.relative_to(ROOT).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest() for path in (dest, metadata(dest))}
    for name in ('OfflinePartnerDetailPresentationServices', 'OfflinePartnerDetailWindowServices',
                 'OfflinePartnerRewardUiPresentation', 'PartnerDetailWindowView'):
        adapter = dest.with_name(name + '.cs')
        for path in (adapter, Path(str(adapter) + '.meta')):
            outputs[path.relative_to(ROOT).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
    proof = dict(manifest='884_156692', inputs=inputs, methods=methods, outputs=outputs,
        generator_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        validation_sources={'scripts/fixtures/partner_detail_window_validation.cs': hashlib.sha256(
            (ROOT / 'scripts/fixtures/partner_detail_window_validation.cs').read_bytes()).hexdigest()},
        adaptations=['Typed projections preserve all NPC fields and friendship animations/talk IDs.',
            'Selected Chinese client uses CN=0; callers can pass every retained original region.',
            'Original live-before-static and per-kind region-before-default branches retained.',
            'Diagnostics use Unity logging; resource selection and return order remain original.',
            'Local Windows device tier retains DesktopDevice/DeviceUtility memory thresholds; live controller keeps its serialized cache dictionary.'])
    receipt.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n','utf8', newline='\n')


if __name__ == '__main__':
    main()
