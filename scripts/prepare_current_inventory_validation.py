"""Create original-object geometry and full-mip GPU fixtures for installed inventory art."""
import argparse
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from official_source import OfficialSource
from install_current_inventory_sprites import PROOF, verify_output


def prepare(output):
    receipt_raw = (ROOT/PROOF).read_bytes()
    receipt = json.loads(receipt_raw)
    source = OfficialSource()
    if receipt['manifest'] != source.version or receipt['apk_sha256'] != source.apk_sha256:
        raise ValueError('Install selected inventory native Sprites first')
    for row in receipt['outputs']+receipt['shared_dependencies']:
        verify_output(row)
    textures = {row['native']['identity']: row for row in receipt['textures']}
    sprites = []
    for row in receipt['aliases']:
        native = row['native']
        sprites.append(dict(native['expected'], alias=row['alias'], path=row['path'],
                            texture_path=textures[native['texture']]['path']))
    expected_textures = []
    for row in textures.values():
        native = row['native']
        original = native['original_fields']
        settings = original['m_TextureSettings']
        expected_textures.append(dict(path=row['path'], width=original['m_Width'], height=original['m_Height'],
            format=native['format'], readable=original['m_IsReadable'], mip_count=original['m_MipCount'],
            filter=settings['m_FilterMode'], aniso=settings['m_Aniso'],
            wrapU=settings['m_WrapU'], wrapV=settings['m_WrapV'],
            mips=[dict(level=mip['level'], sha256=mip['rgba_sha256']) for mip in native['mips']]))
    output.mkdir(parents=True, exist_ok=True)
    target = output/'inventory-native-fixture.json'
    target.write_text(json.dumps(dict(manifest=source.version, receipt_sha256=hashlib.sha256(receipt_raw).hexdigest(),
                                     sprites=sprites, textures=expected_textures),
                                 ensure_ascii=False, indent=2)+'\n', 'utf8')
    print('Prepared inventory fixture:', len(sprites), 'bindings,', len(textures), 'full-mip textures', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    prepare(parser.parse_args().output.resolve())
