"""Verify recorded converter source bytes without retaining source-code copies."""
import hashlib
from pathlib import Path
import subprocess

from official_apk import ROOT


def verify_recorded_source(row):
    path = ROOT/row['path']
    expected = row['sha256']
    if path.is_file() and hashlib.sha256(path.read_bytes()).hexdigest() == expected:
        return
    # Converted resources retain the generator that actually produced them.
    # A later tool edit/retirement does not regenerate those installed bytes.
    # Only converter code may use historical evidence; runtime assets and
    # original inputs must continue to match their live files exactly.
    if not path.resolve().is_relative_to((ROOT/'scripts').resolve()) or path.suffix not in ('.py','.ps1','.cs'):
        raise ValueError('Recorded source differs: '+row['path'])
    commits = subprocess.check_output(['git','log','--all','--format=%H','--',row['path']],cwd=ROOT,text=True).splitlines()
    seen = set()
    for commit in commits:
        result = subprocess.run(['git','show',commit+':'+row['path']],cwd=ROOT,capture_output=True)
        if result.returncode:
            continue
        raw = result.stdout
        digest = hashlib.sha256(raw).hexdigest()
        if digest in seen:
            continue
        seen.add(digest)
        # Git stores text with LF; recorded Windows converter identities can
        # refer to the corresponding CRLF checkout. Verify either exact byte
        # representation without changing the receipt's original hash.
        if digest == expected or hashlib.sha256(raw.replace(b'\r\n',b'\n').replace(b'\n',b'\r\n')).hexdigest() == expected:
            return
    raise ValueError('Recorded converter source is absent from Git: '+row['path'])
