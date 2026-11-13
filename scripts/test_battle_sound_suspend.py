"""Observe original Windows Wwise suspension without OS focus or audible events."""
import argparse
import ctypes
import hashlib
import json
import os
from pathlib import Path
import time

ROOT = Path(__file__).resolve().parents[1]
SDK = ROOT / '.tools/wwise/UnityIntegration-2022.1.18.8567.3199'


def verify(variant, identities):
    dll = SDK / f'Wwise/API/Runtime/Plugins/Windows/x86_64/{variant}/AkSoundEngine.dll'
    raw = dll.read_bytes()
    digest = hashlib.sha256(raw).hexdigest()
    expected = identities[dll.relative_to(ROOT).as_posix()]
    if len(raw) != expected['size'] or digest != expected['sha256']:
        raise ValueError('Original Wwise DLL identity changed: ' + variant)
    engine = ctypes.CDLL(str(dll))

    def bind(name, result, arguments):
        function = getattr(engine, 'CSharp_' + name)
        function.restype = result
        function.argtypes = arguments
        return function

    new = bind('new_AkInitializationSettings', ctypes.c_void_p, [])
    delete = bind('delete_AkInitializationSettings', None, [ctypes.c_void_p])
    initialize = bind('Init', ctypes.c_int, [ctypes.c_void_p])
    terminate = bind('Term', None, [])
    mute = bind('SetOutputVolume', ctypes.c_int, [ctypes.c_uint64, ctypes.c_float])
    suspend = bind('Suspend__SWIG_1', ctypes.c_int, [ctypes.c_bool])
    wake = bind('WakeupFromSuspend__SWIG_1', ctypes.c_int, [])
    render = bind('RenderAudio__SWIG_1', ctypes.c_int, [])
    tick = bind('GetSampleTick', ctypes.c_uint64, [])

    def check(value):
        if value != 1:
            raise ValueError('Original Wwise call returned AKRESULT ' + str(value))

    def delta():
        # Let the audio thread consume the prior queued operation before measuring.
        time.sleep(.12)
        before = tick()
        time.sleep(.2)
        return tick() - before

    settings = new()
    initialized = False
    result = dict(variant=variant, sha256=digest, events_posted=0,
                  settings='Unmodified native AkInitializationSettings defaults')
    try:
        check(initialize(settings))
        initialized = True
        check(mute(0, 0))
        check(render())
        result['initial_delta'] = delta()
        for render_anyway in (False, True):
            check(suspend(render_anyway))
            check(render())
            result[f'suspend_{render_anyway}_delta'] = delta()
            check(wake())
            check(render())
            result[f'resume_{render_anyway}_delta'] = delta()
        if result['initial_delta'] <= 0 or result['suspend_False_delta'] != 0:
            raise ValueError('Original sample clock did not stop during Suspend(false).')
        if any(result[key] <= 0 for key in
               ('suspend_True_delta', 'resume_False_delta', 'resume_True_delta')):
            raise ValueError('Original sample clock did not continue or resume.')
        return result
    finally:
        if initialized:
            wake()
            render()
            terminate()
        delete(settings)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    workspace_value = os.environ.get('ZJCS_VALIDATION_DIR')
    if not workspace_value:
        raise ValueError('Set ZJCS_VALIDATION_DIR to a marked validation workspace.')
    workspace = Path(workspace_value).resolve()
    output = args.output.resolve()
    if not (workspace / '.zjcs-validation.json').is_file() or workspace not in output.parents:
        raise ValueError('Write results only inside the marked validation workspace.')
    if output.exists():
        raise FileExistsError(output)
    source = ROOT / 'assets/reference/battle-animation/wwise-windows-integration-source-manifest.json'
    identities = {entry['path']: entry for entry in json.loads(source.read_bytes())['files']}
    result = dict(passed=False, variants=[], error='')
    try:
        for variant in ('Profile', 'Release'):
            result['variants'].append(verify(variant, identities))
        result['passed'] = True
    except Exception as error:
        result['error'] = str(error)
        raise
    finally:
        output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Original Wwise Profile/Release sample clocks: Suspend(false) stops; '
          'Suspend(true) continues; both wake successfully. No sound events posted.')


if __name__ == '__main__':
    main()
