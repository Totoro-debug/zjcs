"""Resolve original on-demand appearance GUIDs against the selected AssetTable."""
from official_apk import ROOT
from official_manifest import read_asset_manifest
from prepare_unity_bundle_inputs import collect, serialized_files
from prepare_unity_map13_sources import sha


def collect_textures(apk, schemas):
    raw = apk.source.manifest_bytes("DefaultPackage", apk.archive)
    assets = read_asset_manifest(raw)
    by_guid = {}
    for asset in assets:
        if asset["guid"]:
            if asset["guid"] in by_guid:
                raise ValueError("Original AssetTable GUID is ambiguous")
            by_guid[asset["guid"]] = asset
    processed, bindings, roots = set(), [], set()
    while pending := sorted(set(apk.loaded) - processed):
        for name in pending:
            processed.add(name)
            for file in serialized_files(apk.loaded[name]):
                for obj in file.objects.values():
                    if obj.type.name != "MonoBehaviour":
                        continue
                    header = obj.read(check_read=False)
                    if not header.m_Script.m_PathID:
                        continue
                    script = header.m_Script.read()
                    if (script.m_Namespace, script.m_ClassName) != ("AppearanceSystem.Runtime", "YooAssetTextureLoader"):
                        continue
                    fields, _ = schemas.read(obj)
                    for index, mapping in enumerate(fields["placeholderMap"]):
                        for role in ("targetTextureReference", "dyeTextureReference"):
                            guid = mapping[role]["AssetReference"]["mAssetGuid"]
                            if not guid:
                                continue
                            if guid not in by_guid:
                                raise ValueError("Resolve original dynamic appearance GUID: " + guid)
                            asset = by_guid[guid]
                            target = apk.asset(asset["bundle"], asset["path"])
                            if target.type.name != "Texture2D":
                                raise ValueError("Original dynamic appearance GUID is not a Texture2D")
                            roots.add(asset["bundle"])
                            bindings.append(dict(cab=file.name, path_id=str(obj.path_id), mapping=index, role=role,
                                asset=asset, source=apk.provenance(target), payload_sha256=sha(target.get_raw_data())))
        _, missing = collect(apk, sorted(roots)) if roots else ({}, {})
        if missing:
            raise ValueError("Original dynamic texture CAB dependency is missing: " + str(missing))
    return dict(manifest_sha256=sha(raw), asset_count=len(assets), roots=sorted(roots), bindings=bindings)
