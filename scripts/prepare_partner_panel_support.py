"""Port the selected original legacy-animation state configuration unchanged."""
from pathlib import Path
from official_apk import ROOT
from prepare_partner_input_dialog import metadata


def prepare():
    from prepare_partner_panel_tipdots import prepare as prepare_tipdots
    output = ROOT / 'unity/Assets/Game/Runtime/Official/PartnersUI'
    inputs, outputs = [], []
    for name in ('StateAnimationConfig', 'StateAnimationData'):
        source = ROOT / '.tools/source-packages/official-code/framework-source' / (name + '.cs')
        selected = ROOT / '.tools/source-packages/current/884_156692/official-code/framework-source' / (name + '.cs')
        if source.read_bytes() != selected.read_bytes(): raise ValueError('Selected state-animation code differs')
        body = source.read_text('utf8').replace('IStateConfig', 'IWorldStateConfig').replace('StateConfigValue', 'WorldStateConfigValue')
        body = '// Selected original state-animation support; see partner-panel-native-source-manifest.json.\n' + \
            'namespace Zjcs.Official.PartnersUI\n{\nusing Zjcs.Official.WorldUI;\n' + body + '\n}\n'
        target = output / source.name
        target.write_text(body, 'utf8', newline='\n')
        inputs.extend((source, selected)); outputs.extend((target, metadata(target)))
    more_inputs, more_outputs = prepare_tipdots()
    return inputs + more_inputs, outputs + more_outputs


if __name__ == '__main__':
    prepare()
