"""Generate the local Resources catalog for Home building TipDotNodeGraph assets."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT


RECEIPT = ROOT / 'assets/reference/home-tip-dot-catalog-source-manifest.json'
ASSET = ROOT / 'unity/Assets/Game/Resources/Home/HomeTipDots/Assets.asset'
SCRIPT = ROOT / 'unity/Assets/Game/Runtime/Offline/HomeTipDots/OfflineHomeTipDotAssets.cs'
ASSEMBLY = ROOT / 'unity/Assets/Game/Runtime/Offline/HomeTipDots/Zjcs.Offline.HomeTipDots.asmdef'
SERVICES = ROOT / 'unity/Assets/Game/Runtime/Offline/HomeTipDots/OfflineHomeTipDotServices.cs'
RUNTIME_HELPER = ROOT / 'unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.HomeTipDots.cs'
MANIFEST_SOURCES = (
    ROOT / 'assets/reference/home-bed-native-source-manifest.json',
    ROOT / 'assets/reference/home-cart-native-source-manifest.json',
    ROOT / 'assets/reference/home-cart-windows-native-source-manifest.json',
)
GRAPH_SCRIPT_GUID = '8e5b2be808a35cf8a8914701067cfea8'


def sha(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def read_guid(meta: Path) -> str:
    match = re.search(r'^guid: ([0-9a-f]{32})\s*$', meta.read_text('utf-8'), re.M)
    if not match:
        raise ValueError('GUID missing: ' + str(meta))
    return match.group(1)


def ensure_meta(path: Path, importer: str, *, folder: bool = False) -> tuple[str, bytes]:
    token = hashlib.md5(('zjcs:home-tip-dot-catalog:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
    meta = Path(str(path) + '.meta')
    if meta.is_file():
        token = read_guid(meta)
    text = (
        'fileFormatVersion: 2\n'
        + 'guid: ' + token + '\n'
        + ('folderAsset: yes\n' if folder else '')
        + importer + ':\n'
        + '  externalObjects: {}\n'
    ).encode('utf-8')
    if not meta.is_file() or meta.read_bytes().replace(b'\r\n', b'\n') != text:
        meta.write_bytes(text)
    return token, meta.read_bytes()


def prepare():
    offline_folder = SCRIPT.parent
    ensure_meta(offline_folder, 'DefaultImporter', folder=True)
    ensure_meta(ASSEMBLY, 'AssemblyDefinitionImporter')
    script_guid, _ = ensure_meta(SCRIPT, 'MonoImporter')
    ensure_meta(SERVICES, 'MonoImporter')
    ensure_meta(RUNTIME_HELPER, 'MonoImporter')

    inputs = {}
    entries = []
    seen_names = set()
    manifest_version = None

    for source_path in MANIFEST_SOURCES:
        raw = source_path.read_bytes()
        inputs[source_path.relative_to(ROOT).as_posix()] = sha(raw)
        data = json.loads(raw.decode('utf-8'))
        manifest_version = manifest_version or data['manifest']
        if data['manifest'] != manifest_version:
            raise ValueError('Mismatched Home native manifest version: ' + str(source_path))
        installed_root = data.get('destination') or ''
        for rel, expected_sha in sorted(data.get('outputs', {}).items()):
            if '/Graph/' not in ('/' + rel) or not rel.endswith('.asset'):
                continue
            asset_path = ROOT / rel
            if not asset_path.is_file() and installed_root:
                asset_path = ROOT / installed_root / rel
            if not asset_path.is_file():
                prefab_rows = data.get('prefabs', [])
                if prefab_rows:
                    asset_path = (ROOT / prefab_rows[0]['prefab']).parent / rel
            if not asset_path.is_file():
                suffix = '/' + rel
                matches = [ROOT / k for k in data.get('installed_output_sha256', {}) if k.endswith(suffix)]
                if matches:
                    asset_path = matches[0]
            asset_raw = asset_path.read_bytes()
            if sha(asset_raw) != expected_sha:
                raise ValueError('Home graph payload differs from source receipt: ' + rel)
            text = asset_raw.decode('utf-8-sig', errors='ignore')
            if f'guid: {GRAPH_SCRIPT_GUID},' not in text:
                continue
            name_m = re.search(r'(?m)^\s*m_Name:\s*(.+?)\s*$', text)
            type_m = re.search(r'(?m)^\s*Type:\s*(\d+)\s*$', text)
            scope_m = re.search(r'(?m)^\s*Scope:\s*(\d+)\s*$', text)
            if not name_m or not type_m or not scope_m:
                raise ValueError('Invalid TipDotNodeGraph asset: ' + str(asset_path))
            graph_name = name_m.group(1)
            if graph_name in seen_names:
                raise ValueError('Duplicate Home TipDotGraph name: ' + graph_name)
            seen_names.add(graph_name)
            meta_path = Path(str(asset_path) + '.meta')
            graph_guid = read_guid(meta_path)
            inputs[asset_path.relative_to(ROOT).as_posix()] = sha(asset_raw)
            inputs[meta_path.relative_to(ROOT).as_posix()] = sha(meta_path.read_bytes())
            entries.append(dict(
                GraphName=graph_name,
                GraphPath='Home/' + graph_name,
                GraphType=int(type_m.group(1)),
                GraphScope=int(scope_m.group(1)),
                GraphGuid=graph_guid,
                GraphFile=asset_path.relative_to(ROOT).as_posix(),
            ))

    if not entries:
        raise ValueError('No verified Home TipDotNodeGraph entries discovered')

    lines = [
        '%YAML 1.1',
        '%TAG !u! tag:unity3d.com,2011:',
        '--- !u!114 &11400000',
        'MonoBehaviour:',
        '  m_ObjectHideFlags: 0',
        '  m_CorrespondingSourceObject: {fileID: 0}',
        '  m_PrefabInstance: {fileID: 0}',
        '  m_PrefabAsset: {fileID: 0}',
        '  m_GameObject: {fileID: 0}',
        '  m_Enabled: 1',
        '  m_EditorHideFlags: 0',
        f'  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}',
        '  m_Name: Assets',
        '  m_EditorClassIdentifier:',
        '  entries:',
    ]
    for entry in entries:
        lines.extend([
            f'  - GraphName: {json.dumps(entry["GraphName"], ensure_ascii=False)}',
            f'    GraphPath: {json.dumps(entry["GraphPath"], ensure_ascii=False)}',
            f'    GraphType: {entry["GraphType"]}',
            f'    GraphScope: {entry["GraphScope"]}',
            f'    Graph: {{fileID: 11400000, guid: {entry["GraphGuid"]}, type: 2}}',
        ])

    ASSET.parent.mkdir(parents=True, exist_ok=True)
    ensure_meta(ASSET.parent, 'DefaultImporter', folder=True)
    asset_bytes = ('\n'.join(lines) + '\n').encode('utf-8')
    ASSET.write_bytes(asset_bytes)
    asset_guid, asset_meta_bytes = ensure_meta(ASSET, 'NativeFormatImporter')

    outputs = {
        ASSET.relative_to(ROOT).as_posix(): sha(asset_bytes),
        Path(str(ASSET) + '.meta').relative_to(ROOT).as_posix(): sha(asset_meta_bytes),
    }
    inputs[Path(__file__).relative_to(ROOT).as_posix()] = sha(Path(__file__).read_bytes())

    receipt = dict(
        manifest=manifest_version,
        inputs=inputs,
        outputs=outputs,
        entries=entries,
        asset_guid=asset_guid,
        generator_sha256=sha(Path(__file__).read_bytes()),
    )
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf-8', newline='\n')
    print('Prepared Home TipDot catalog:', len(entries), 'graphs')


if __name__ == '__main__':
    prepare()
