"""Recover type descriptors from the selected APK's unpacked ARM64 image.

The supplied image is produced by recover_official_native.py at the inspected
loader boundary. Dummy DLLs describe original types; their methods are stubs.
They must never be used as runtime replacements.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import struct
import subprocess

from official_apk import ROOT
from official_source import OfficialSource
from validation_workspace import temporary_validation_directory


def record(path):
    with path.open('rb') as stream:
        digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    return dict(path=path.relative_to(ROOT).as_posix(), bytes=path.stat().st_size, sha256=digest)


def parser_view(raw):
    """Describe an already unpacked flat VM image, without changing method bytes."""
    result = bytearray(raw)
    phoff = struct.unpack_from('<Q', raw, 32)[0]
    count = struct.unpack_from('<H', raw, 56)[0]
    if phoff != 64 or not 1 <= count <= 32:
        raise ValueError('Unrecognized recovered program headers')
    segments = [list(struct.unpack_from('<IIQQQQQQ', raw, phoff+i*56)) for i in range(count)]
    loads = [tuple(s) for s in segments if s[0] == 1]
    if max(s[3]+s[5] for s in loads) != len(raw):
        raise ValueError('Recovered file does not cover original file-backed load segments')
    for segment in segments:
        if segment[0] == 2:
            matches = [s for s in loads if s[2] <= segment[2] < s[2]+s[5]]
            if len(matches) != 1:
                raise ValueError('Original dynamic table has no unique load mapping')
            segment[3] = segment[4] = matches[0][3]+segment[2]-matches[0][2]
        segment[2] = segment[3]
    ident = b'\x7fELF\x02\x01\x01'+bytes(9)
    result[:64] = struct.pack('<16sHHIQQQIHHHHHH', ident, 3, 183, 1, 0, 64, 0, 0, 64, 56, count, 64, 0, 0)
    for i, segment in enumerate(segments):
        struct.pack_into('<IIQQQQQQ', result, 64+i*56, *segment)
    end = 64+56*count
    if result[end:] != raw[end:]:
        raise AssertionError('Parser view changed native payload')
    return result


def inspect_loader_boundary(raw):
    # Inspected ARM64 loader sequence: store the decoded relocation tables in
    # X19, call the unpacker, then test its result before import initialization.
    # Its instructions are identical in the inspected APKs; its address is not.
    signature = bytes.fromhex(
        '680e00f9e81340f968a600f9e82340f9681200f9e82740f9681a00f9'
        'e083c53c6026803de083c63c602a803de83f40f9682200f9e84340f9'
        '682600f987feff97f5031faa600d0036e00313aa77000094e16340f9'
        'e00313aacd0000946842453908010037e00313aaca020094f5031faa'
        '000c0036685241b908010032685201b9')
    offset = raw.find(signature)
    if offset < 0 or raw.find(signature, offset + 1) >= 0:
        raise ValueError('Inspect the different or ambiguous ARM64 loader first')
    if raw[:7] != b'\x7fELF\x02\x01\x01' or struct.unpack_from('<H', raw, 18)[0] != 183:
        raise ValueError('Expected the original AArch64 ELF')
    phoff = struct.unpack_from('<Q', raw, 32)[0]
    count = struct.unpack_from('<H', raw, 56)[0]
    segments = [struct.unpack_from('<IIQQQQQQ', raw, phoff + i * 56) for i in range(count)]
    matches = [s for s in segments if s[0] == 1 and s[1] & 1
               and s[2] <= offset and offset + len(signature) <= s[2] + s[5]]
    if len(matches) != 1:
        raise ValueError('The inspected loader is not in one executable load segment')
    return matches[0][3] + offset - matches[0][2] + 64, hashlib.sha256(signature).hexdigest()


def prepare(image, recovery_log):
    source = OfficialSource()
    base = ROOT/'.tools/source-packages/current'/source.version
    output = base/'official-code/aot-metadata'
    if output.exists():
        raise FileExistsError('Keep the existing AOT recovery')
    manifest = json.loads((base/'code-manifest.json').read_bytes())
    original = base/'official-code/native/arm64-v8a/libil2cpp.so'
    library = next(row for row in manifest['native_libraries'] if row['apk_entry'] == 'lib/arm64-v8a/libil2cpp.so')
    if record(original)['sha256'] != library['sha256']:
        raise ValueError('Original ARM64 input differs from extracted APK')
    boundary, boundary_signature = inspect_loader_boundary(original.read_bytes())
    recovery = recovery_log.read_text('utf-8-sig')
    relocations = re.findall(r'^Internal relative relocations (\d+)\s*$', recovery, re.M)
    images = re.findall(r'^Recovered native virtual image .+ bytes (\d+)\s*$', recovery, re.M)
    if (f'Reached requested original-code boundary {hex(boundary)}' not in recovery
            or len(relocations) != 1 or not 0 < int(relocations[0]) <= 5_000_000
            or len(images) != 1 or int(images[0]) != image.stat().st_size):
        raise ValueError('Recovery did not reach the inspected loader boundary and restore its relocations')
    output.mkdir(parents=True)
    shutil.copyfile(recovery_log, output/'recovery.log')
    shutil.copyfile(image, output/'native-image.so')
    (output/'analysis-elf.so').write_bytes(parser_view(image.read_bytes()))
    metadata = base/'official-code/global-metadata.dat'
    tool = ROOT/'.tools/il2cppdumper'
    config = json.loads((tool/'config.json').read_bytes())
    config.update(RequireAnyKey=False, ForceDump=True, NoRedirectedPointer=True, GenerateStruct=False)
    with temporary_validation_directory('aot-metadata-') as temporary:
        runner = Path(temporary)/'runner'
        shutil.copytree(tool, runner, ignore=shutil.ignore_patterns('DummyDll', 'dump.cs', 'script.json', 'stringliteral.json', 'il2cpp.h'))
        (runner/'config.json').write_text(json.dumps(config), 'utf8')
        environment = dict(os.environ, DOTNET_ROLL_FORWARD='LatestMajor')
        process = subprocess.run([str(ROOT/'.tools/dotnet/dotnet.exe'), str(runner/'Il2CppDumper.dll'),
                                  str(output/'analysis-elf.so'), str(metadata), str(output)],
                                 input='0\n', text=True, capture_output=True, env=environment, cwd=runner)
        print(process.stdout, flush=True)
        if process.returncode or not (output/'DummyDll').is_dir():
            raise RuntimeError('AOT metadata extraction failed: '+process.stderr)
    headers = struct.unpack_from('<64I', metadata.read_bytes())
    text = (output/'dump.cs').read_text('utf-8-sig')
    types = {int(x) for x in re.findall(r'TypeDefIndex: (\d+)', text)}
    count = headers[41]//88
    dlls = list((output/'DummyDll').glob('*.dll'))
    if types != set(range(count)) or len(dlls) != headers[43]//40:
        raise ValueError('Incomplete original AOT type coverage')
    result = dict(manifest=source.version, apk_sha256=source.apk_sha256, source=record(original),
                  metadata=record(metadata), generator=record(Path(__file__)),
                  recovery_generator=record(ROOT/'scripts/recover_official_native.py'),
                  dumper=record(tool/'Il2CppDumper.dll'), dumper_config=config,
                  loader_boundary=hex(boundary), loader_signature_sha256=boundary_signature,
                  relative_relocations=int(relocations[0]), recovery_log=record(output/'recovery.log'),
                  boundary_scope='Inspected unpack-complete call site in selected ARM64 loader; before import/thread initialization.',
                  parser_changes='ELF header and program headers only; native code bytes unchanged.',
                  recovered_types=count, metadata_images=headers[43]//40,
                  limitations=['Dummy DLL method bodies are stubs, for converter type resolution only; never ship as game implementations.'],
                  outputs=[record(p) for p in sorted(output.rglob('*')) if p.is_file()])
    (output/'source-manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', 'utf8')
    print('Verified complete AOT type coverage:', count, 'assemblies:', len(dlls))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--recovered-image', type=Path, required=True)
    parser.add_argument('--recovery-log', type=Path, required=True)
    args = parser.parse_args()
    prepare(args.recovered_image.resolve(), args.recovery_log.resolve())
