"""Verify the recorded renderer method bytes against the selected official APK."""
import hashlib
import json
import struct
import zipfile
from pathlib import Path

from official_apk import ROOT
from official_source import OfficialSource
from recover_official_native import NativeRecovery


class LoaderProbe(NativeRecovery):
    def invoke(self, name, args):
        # The protected loader has already restored code by this point; stop
        # before emulating the application's thread-local runtime initialization.
        if name == 'pthread_key_create':
            self.reached_stop = True
            self.uc.emu_stop()
            return 0
        return super().invoke(name, args)


def verify():
    source = OfficialSource()
    proof = json.loads((ROOT / 'assets/reference/home-airship-render-mesh-source-manifest.json').read_text(encoding='utf8'))['source']
    with source.apk.open('rb') as stream:
        if hashlib.file_digest(stream, 'sha256').hexdigest() != source.apk_sha256 or source.apk_sha256 != proof['apk_sha256']:
            raise ValueError('Selected APK differs from the renderer source receipt.')
    with zipfile.ZipFile(source.apk) as archive:
        native_hash = hashlib.sha256(archive.read(proof['native_entry'])).hexdigest()
    if native_hash != proof['native_sha256']:
        raise ValueError('Selected native library differs from the renderer source receipt.')
    code = ROOT / '.tools/source-packages/current' / source.version / 'official-code'
    dump = code / 'aot-metadata/dump.cs'
    if hashlib.sha256(dump.read_bytes()).hexdigest() != proof['metadata_dump_sha256']:
        raise ValueError('Selected native metadata differs from the renderer source receipt.')
    runtime = LoaderProbe(code / 'native/arm64-v8a/libil2cpp.so', native_hash)
    count = runtime.tags['DT_INIT_ARRAYSZ'] // 8
    constructors = struct.unpack('<' + 'Q' * count, runtime.uc.mem_read(runtime.tags['DT_INIT_ARRAY'], count * 8))
    for address in constructors:
        runtime.call(address, 20_000_000_000)
        if runtime.reached_stop:
            break
    if not runtime.reached_stop:
        raise RuntimeError('The verified loader did not reach the inspection boundary.')
    raw = bytes(runtime.uc.mem_read(int(proof['address_start'], 16), proof['length']))
    if hashlib.sha256(raw).hexdigest() != proof['recovered_bytes_sha256']:
        raise ValueError('Recovered renderer instructions differ from the source receipt.')
    print('Verified original airship renderer methods:', len(raw), 'bytes')


if __name__ == '__main__':
    verify()
