"""Migrate the original activity countdown coroutine and its native view."""
import hashlib
import json
import re
from pathlib import Path

from official_apk import ROOT
from prepare_partner_input_dialog import metadata


def main():
    source = ROOT / '.tools/source-packages/current/884_156692/official-code/client-source'
    target = ROOT / 'unity/Assets/Game/Runtime/Official/ActivityBattleUI'
    inputs, outputs = {}, {}
    for name in ('ACountdownView', 'CountdownView', 'CountdownText'):
        path = source / (name + '.cs')
        inputs[path.relative_to(ROOT).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
        text = path.read_text('utf8')
        for using in ('using Framework;\n', 'using Framework.Logging;\n',
                      'using Framework.Logging.InterpolatedStringHandler;\n'):
            text = text.replace(using, '')
        if name == 'CountdownText':
            start = text.index('\t\t\tGLoggerErrorInterpolatedStringHandler message =')
            end = text.index('\n\t\t}', start)
            text = text[:start] + '\t\t\tDebug.LogError("Countdown 必须在节点被激活时显示！[" + gameObject.name + "]", this);' + text[end:]
        elif name == 'CountdownView':
            old = 'new CultureTimeSpanArg(timeSpan, mFormat)'
            assert text.count(old) == 1
            text = text.replace(old, 'new WorldCultureTimeSpanArg(timeSpan, mFormat, mCountdownText.Localizer)')
            text = re.sub(r'\bObject\b', 'UnityEngine.Object', text)
        text = '\n'.join(line for line in text.splitlines() if '//IL_' not in line) + '\n'
        aliases = ('using Zjcs.Official.WorldUI;\n'
                   'using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;\n'
                   'using ETimeSpanFormatType = Zjcs.Official.WorldUI.WorldTimeSpanFormatType;\n') if name == 'CountdownView' else ''
        output = target / (name + '.cs')
        output.write_text('// Migrated selected original countdown; see activity-countdown-runtime-source-manifest.json.\n'
                          + aliases + 'namespace Zjcs.Official.ActivityBattleUI\n{\n' + text + '}\n', 'utf8', newline='\n')
        for item in (output, metadata(output)):
            outputs[item.relative_to(ROOT).as_posix()] = hashlib.sha256(item.read_bytes()).hexdigest()
    formatter = ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI/WorldCultureTimeSpanArg.cs'
    inputs[formatter.relative_to(ROOT).as_posix()] = hashlib.sha256(formatter.read_bytes()).hexdigest()
    inputs[Path(__file__).relative_to(ROOT).as_posix()] = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
    receipt = dict(manifest='884_156692', inputs=inputs, outputs=outputs,
        adaptation='Original serialized fields, Show overloads, scaled WaitForSeconds intervals, initial do/while update, '
                   'remaining/duration ratio and disable cancellation retained. Logging uses Unity; '
                   'CultureTimeSpanArg uses the existing migrated formatter and the view localizer.')
    (ROOT / 'assets/reference/daily-challenges/activity-countdown-runtime-source-manifest.json').write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')


if __name__ == '__main__': main()
