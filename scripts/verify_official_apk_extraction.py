"""Verify every unpacked APK entry against the selected archive, retaining hashes."""
import hashlib
import json
from pathlib import Path
import zipfile

from official_source import OfficialSource, ROOT


def verify():
    source = OfficialSource()
    extracted = source.apk.parent / 'unpacked/apk'
    with source.apk.open('rb') as stream:
        if hashlib.file_digest(stream, 'sha256').hexdigest() != source.apk_sha256:
            raise ValueError('Selected APK hash differs')
    files = []
    with zipfile.ZipFile(source.apk) as archive:
        names = set()
        for entry in archive.infolist():
            if entry.is_dir():
                continue
            target = (extracted / entry.filename).resolve()
            if not target.is_relative_to(extracted.resolve()) or entry.filename.casefold() in names:
                raise ValueError('Unsafe or ambiguous APK entry: ' + entry.filename)
            names.add(entry.filename.casefold())
            with archive.open(entry) as stream:
                expected = hashlib.file_digest(stream, 'sha256').hexdigest()
            with target.open('rb') as stream:
                actual = hashlib.file_digest(stream, 'sha256').hexdigest()
            if expected != actual or target.stat().st_size != entry.file_size:
                raise ValueError('Extracted APK entry differs: ' + entry.filename)
            files.append(dict(entry=entry.filename, bytes=entry.file_size, sha256=actual))
            if len(files) % 2000 == 0:
                print('Verified APK entries:', len(files), flush=True)
    receipt = dict(manifest=source.version, apk_sha256=source.apk_sha256,
                   generator_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
                   extraction_root=extracted.relative_to(ROOT).as_posix(),
                   files=files, completed=True)
    path = source.prepared_path / 'apk-extraction-source-manifest.json'
    path.write_text(json.dumps(receipt, indent=2) + '\n', 'utf8')
    print('Verified complete APK extraction:', len(files), flush=True)


if __name__ == '__main__':
    verify()
