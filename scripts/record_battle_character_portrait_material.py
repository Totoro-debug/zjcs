"""Verify the original atlas clipping value used by shared native head photography."""
from __future__ import annotations

import hashlib
import json
import math
import os
from pathlib import Path
import re
import shutil

from current_component_schemas import CurrentSchemas
from official_apk import Apk
from UnityPy.classes import PPtr

ROOT = Path(__file__).resolve().parents[1]
RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Presentation/Character/OfficialBattleCharacterPortraits.cs'
RECEIPT = ROOT / 'assets/reference/battle-hud/character-portrait-material-source-manifest.json'


def record(path: Path) -> dict:
    raw = path.read_bytes()
    result = dict(path=path.relative_to(ROOT).as_posix())
    if path.suffix in ('.cs', '.py'):
        raw = raw.replace(b'\r\n', b'\n')
        result['hashLineEndings'] = 'LF'
    result['sha256'] = hashlib.sha256(raw).hexdigest()
    return result


def build():
    local_dotnet = ROOT / '.tools/dotnet/dotnet.exe'
    if shutil.which('dotnet') is None and local_dotnet.is_file():
        os.environ['PATH'] = str(local_dotnet.parent) + os.pathsep + os.environ.get('PATH', '')
    apk = Apk.current()
    if apk.version != '884_156692':
        raise ValueError('Review the original character material version before reproduction')
    schemas = CurrentSchemas()
    sources = []
    material_values = []
    counts = {}

    def source(obj, **fields):
        sources.append(dict(source=apk.provenance(obj),
            payload_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(), **fields))

    for sex in ('male', 'female'):
        model = apk.asset('appearanceassets_character_model_' + sex + '_1',
            '/character_' + sex.title() + '_1_SkeletonData.asset')
        model_fields = schemas.read(model)
        source(model, sex=sex, atlasAssets=model_fields['atlasAssets'])
        counts[sex] = 0
        for reference in model_fields['atlasAssets']:
            atlas = PPtr(**reference, assetsfile=model.assets_file).deref()
            atlas_fields = schemas.read(atlas)
            source(atlas, sex=sex, materials=atlas_fields['materials'])
            for reference in atlas_fields['materials']:
                material = PPtr(**reference, assetsfile=atlas.assets_file).deref()
                fields = material.read_typetree()
                height = dict(fields['m_SavedProperties']['m_Floats'])['_WorldClipHeight']
                if not math.isfinite(height):
                    raise ValueError('The original world clipping value is not finite')
                source(material, sex=sex, name=fields['m_Name'], worldClipHeight=height)
                material_values.append(height)
                counts[sex] += 1
    values = set(material_values)
    match = re.search(r'private const float OriginalWorldClipHeight = (-?[0-9.]+)f;',
        RUNTIME.read_text(encoding='utf-8'))
    if len(values) != 1 or match is None or float(match[1]) != next(iter(values)):
        raise ValueError('Shared photography differs from the original atlas clipping values')
    proof = dict(format=1, manifest=apk.version, apk_manifest=apk.source.apk_version,
        apk_sha256=apk.source.apk_sha256, configuration=record(ROOT / 'official-source.json'),
        property='_WorldClipHeight', value=next(iter(values)), material_counts=counts,
        sources=sources, schema=record(ROOT / 'scripts/current_component_schemas.py'),
        runtime=record(RUNTIME), generator=record(Path(__file__).resolve()),
        operation='Read both original player skeleton/atlas references APK-first and verify every referenced material. Restore the shared original world-clipping value on temporary photography renderers while retaining the original studio position, shader programs, textures and existing dye property blocks. No asset payload is generated or modified.',
        limitations=['This receipt establishes only the original material clipping value; complete character materials, motion, physics, parts and all battle presentation remain separate migration work.'])
    RECEIPT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Verified original player atlas clipping:', counts, next(iter(values)))
    return proof


if __name__ == '__main__':
    build()
