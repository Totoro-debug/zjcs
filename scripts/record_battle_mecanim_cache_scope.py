"""Record the selected application's actual bone-cache configuration.

Reads application sources and serialized game assets only, never SDK sources.
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from UnityPy.classes import PPtr
from current_component_schemas import CurrentSchemas
from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest


def identity(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def main():
    apk = Apk.current()
    assert apk.version == '884_156692'
    receipt = ROOT / '.tools/source-packages/current/884_156692/code-manifest.json'
    code = receipt.parent / 'official-code'
    assembly = code / 'managed/decoded/Assembly-CSharp.dll'
    assert identity(assembly)['sha256'] == json.loads(receipt.read_bytes())['assemblies'][assembly.name]['decoded_sha256']
    table_bytes = apk.source.manifest_bytes('DefaultPackage', apk.archive)
    matches = [row for row in read_asset_manifest(table_bytes)
               if row['path'] == 'Assets/ScriptableConfig/Modules/Spine/BoneTransformCacheManagerConfig.asset']
    assert len(matches) == 1
    entry = matches[0]
    config = apk.asset(entry['bundle'], 'BoneTransformCacheManagerConfig.asset')
    fields = CurrentSchemas().read(config)

    def object_identity(obj):
        return dict(source=apk.provenance(obj), payload_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest())

    def dereference(pointer):
        return PPtr(**pointer, assetsfile=config.assets_file).deref()

    profiles = []
    for sex in ('Male', 'Female'):
        skeleton = dereference(fields[sex + 'SkeletonDataAsset'])
        # Only record the original skeleton object identity; do not inspect SDK code.
        clips = []
        for pointer in fields[sex + 'AnimClipList']:
            clip = dereference(pointer)
            clips.append(dict(name=clip.read().m_Name, **object_identity(clip)))
        assert [row['name'] for row in clips] == ['map/idle_01', 'map/run_01']
        profiles.append(dict(sex=sex, skeleton=object_identity(skeleton), clips=clips))

    names = ['BoneTransformCacheManager.cs', 'BoneTransformCacheManagerConfig.cs',
             'IBoneTransformCacheManager.cs', 'SkeletonBoneTransformCache.cs',
             'AppearanceSystem.Runtime/SkeletonMecanimCacheHelper.cs',
             'AppearanceSystem.Runtime/SkeletonAnimationFrameCalculator.cs',
             'AppearanceSystem.Runtime/SkeletonBoneCacheUtils.cs',
             'AppearanceSystem.Runtime/BoneTransformCache.cs', 'AppearanceSystem.Runtime/FrameCache.cs']
    sources = [identity(code / 'client-source' / name) for name in names]
    result = dict(manifest=apk.version, code_receipt=identity(receipt), assembly=identity(assembly),
                  asset_table_sha256=hashlib.sha256(table_bytes).hexdigest(), config_entry=entry,
                  config=dict(fields=fields, **object_identity(config)), profiles=profiles,
                  application_sources=sources, schema_reader=identity(ROOT / 'scripts/current_component_schemas.py'),
                  generator=identity(Path(__file__).resolve()),
                  coverage='Source and serialized configuration evidence only. No runtime migration or verification is claimed.')
    target = ROOT / 'assets/reference/battle-animation/mecanim-cache-scope-source-manifest.json'
    raw = (json.dumps(result, ensure_ascii=False, indent=2) + '\n').encode()
    if not target.exists() or target.read_bytes() != raw:
        target.write_bytes(raw)
    print('Verified current original cache scope: two Map clips per sex; no Game battle clips.')


if __name__ == '__main__':
    main()
