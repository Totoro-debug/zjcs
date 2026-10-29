"""Migrate the complete original property row and its consumed value/format paths."""
import json
import re
from pathlib import Path

from prepare_unity_profession_character_resources import meta, record

ROOT = Path(__file__).resolve().parents[1]
CODE = ROOT / '.tools/source-packages/current/871_156598/official-code'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI'


def block(text, token):
    start = text.index(token)
    begin = text.index('{', start)
    depth = 1
    end = begin + 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[start:end]


def replace(text, old, new):
    if old not in text:
        raise ValueError('Inspect original property dependency: ' + old)
    return text.replace(old, new)


def adapt(text):
    text = re.sub(r'^using (?:Common|Framework(?:\.DependencyInjection)?|R3|UnityEngine.Events);\n', '', text, flags=re.M)
    text = re.sub(r'^\s*//IL_[^\n]*\n', '', text, flags=re.M)
    text = text.replace('LanguageUtils', 'ProfessionPropertyLanguage').replace('PropConfigUtils', 'ProfessionPropertyConfig')
    text = text.replace('PlayerComplexProp', 'ProfessionComplexProp')
    text = text.replace('MultiLanguageText', 'WorldMultiLanguageText').replace('StateSwitcher<', 'WorldStateSwitcher<')
    text = re.sub(r'\bRank\b', 'int', text)
    text = text.replace('LanguageManager.Instance.CurrentLanguage', 'Bindings.Language')
    text = text.replace('LanguageManager.Instance.GetTextFormated', 'Bindings.TextFormatted').replace('LanguageManager.Instance.GetText', 'Bindings.Text')
    text = text.replace('((UnityEvent)PropDetailBtn.onClick).AddListener((UnityAction)ShowPropDetail)', 'PropDetailBtn.onClick.AddListener(ShowPropDetail)')
    text = text.replace('((UnityEvent)PropDetailBtn.onClick).RemoveListener((UnityAction)ShowPropDetail)', 'PropDetailBtn.onClick.RemoveListener(ShowPropDetail)')
    text = text.replace('((UnityEvent)mOpenBtn.onClick).AddListener((UnityAction)OnClick)', 'mOpenBtn.onClick.AddListener(OnClick)')
    text = text.replace('((UnityEvent)mOpenBtn.onClick).RemoveListener((UnityAction)OnClick)', 'mOpenBtn.onClick.RemoveListener(OnClick)')
    text = text.replace('Formula.Instance.', 'ProfessionPropertyFormula.').replace('Formula.Long2Double', 'ProfessionPropertyFormula.Long2Double').replace('Formula.DoubleToString', 'ProfessionPropertyFormula.DoubleToString')
    text = text.replace('Parsers.Instance.PropCfgInfoParser.Get(propType)', 'Bindings.GetProperty(propType)')
    text = text.replace('Parsers.Instance.PropCfgInfoParser.GetPropUnit(propType)', 'Bindings.GetProperty(propType)?.Unit')
    text = text.replace('PropCfgInfo propCfgInfo', 'ProfessionPropertyDefinition propCfgInfo')
    return text


HEADER = '''namespace Zjcs.Official.ProfessionUI
{
using System;
using UnityEngine;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using Object = UnityEngine.Object;
'''


def build():
    receipt = CODE.parent/'code-manifest.json'
    checked = json.loads(receipt.read_bytes())
    assembly = CODE/'managed/decoded/Assembly-CSharp.dll'
    if checked['manifest'] != '871_156598' or record(assembly)['sha256'] != checked['assemblies']['Assembly-CSharp.dll']['decoded_sha256']:
        raise ValueError('Selected original property assembly differs')
    outputs, sources = [], []

    def read(rel):
        p = CODE/rel
        sources.append(record(p))
        return p.read_text(encoding='utf-8')

    def write(name, text, existing=False):
        path = DEST/(name+'.cs')
        path.write_text(HEADER+text+'\n}\n', encoding='utf-8', newline='\n')
        if not existing:
            meta(path)
        outputs.extend((record(path), record(path.with_name(path.name+'.meta'))))

    enum = read('common-source/Common/PropType.cs').replace('namespace Common;\n', '').replace('enum PropType', 'enum ProfessionPropertyType')
    write('ProfessionPropertyType', enum)
    language = read('common-source/Common/LanguageUtils.cs')
    parts = [block(language, '\tpublic '+token) for token in (
        'interface IColorfulPropStr', 'class PropValue :', 'class PropValueOrig :',
        'static string SetTextColorFormatStr(', 'static string FormatPropString(',
        'static string GetEntryNameKey(', 'static string GetEntryDescKey(')]
    lang = adapt('\n\n'.join(parts))
    lang = replace(lang, 'GetEnumName(propType)', 'Bindings.Text(ProfessionPropertyConfig.GetPropTypeName(propType))')
    write('ProfessionPropertyLanguage', 'public static class ProfessionPropertyLanguage\n{\nprivate static IProfessionPropertyBindings Bindings => ProfessionPropertyBindings.Required;\n'+lang+'\n}')
    formula = read('common-source/Common/Formula.cs')
    handler = read('common-source/Common/FormulaHandler.cs')
    parts = [block(formula, '\tpublic static '+token) for token in ('double Long2Double(', 'string DoubleToString(')]
    parts += [block(handler, '\t'+token) for token in (
        'private ICultureNumberFormatter ResolveNumberFormatter(', 'public string ToUpNumberString(',
        'public string NumberToString(', 'public string ToStandardSignificantPercent(')]
    numbers = '\n\n'.join(parts).replace('private ICultureNumberFormatter', 'private static ICultureNumberFormatter').replace('public string', 'public static string')
    write('ProfessionPropertyFormula', 'public static class ProfessionPropertyFormula\n{\n'+numbers+'\n}')
    config = read('common-source/Common/PropConfigUtils.cs').replace('namespace Common;\n', '')
    # The C# type was namespaced for this migration. Keep the original enum's
    # localization name, rather than exposing the renamed port's type name.
    config = config.replace('typeof(PropType).Name', '"PropType"')
    prop_config = read('common-source/PropConfigUtils.cs')
    nested = adapt(block(prop_config, '\tpublic class PropItemValue :'))
    config = replace(config, 'public static class PropConfigUtils\n{', 'public static class ProfessionPropertyConfig\n{\nprivate static IProfessionPropertyBindings Bindings => ProfessionPropertyBindings.Required;\n'+nested+'\n')
    write('ProfessionPropertyConfig', config)
    write('ProfessionComplexProp', adapt(read('client-source/PlayerComplexProp.cs')))
    row = adapt(read('client-source/PlayerPropItem.cs'))
    row = row.replace('class PlayerPropItem', 'class ProfessionPlayerPropItem')
    row = row.replace('private BlessPropBtn mBlessPropBtn', 'private ProfessionBlessPropBtn mBlessPropBtn')
    row = replace(row, 'protected DisposableBag mDisposableBag;', 'private ProfessionResourceOperationBag mDisposableBag;')
    row = replace(row, 'mDisposableBag = default;', 'mDisposableBag = new ProfessionResourceOperationBag();')
    row = replace(row, '\t[Inject]\n\tprotected IResourceLoader ResourceLoader { get; set; }', '''
    private IProfessionPropertyBindings mBindings;
    private IProfessionPropertyBindings Bindings => mBindings ?? ProfessionPropertyBindings.Required;
    public void Configure(IProfessionPropertyBindings bindings)
    { mBindings = bindings ?? throw new ArgumentNullException(nameof(bindings)); }
''')
    row = row.replace('Parsers.Instance.PropCfgInfoParser.Get(mPropType)', 'Bindings.GetProperty(mPropType)')
    row = replace(row, '\t\t\tEntryInfo entryInfo = Parsers.Instance.EntryInfoParser.Get(entryId);\n\t\t\tstring spritePath = entryInfo.Icon;', '\t\t\tstring spritePath = Bindings.GetEntryIcon(entryId);')
    row = replace(row, 'ProfessionPropertyLanguage.TextKeyArgWrap nameStr = new ProfessionPropertyLanguage.TextKeyArgWrap(ProfessionPropertyLanguage.GetEntryNameKey(entryId));', 'ProfessionItemText nameStr = new ProfessionItemText { Key = ProfessionPropertyLanguage.GetEntryNameKey(entryId) };')
    row = replace(row, 'ProfessionPropertyLanguage.TextKeyArgWrap descStr = new ProfessionPropertyLanguage.TextKeyArgWrap(ProfessionPropertyLanguage.GetEntryDescKey(entryId), mDescribleArg);', 'ProfessionItemText descStr = new ProfessionItemText { Key = ProfessionPropertyLanguage.GetEntryDescKey(entryId), Args = new object[] { mDescribleArg } };')
    popup = block(row, '\t\t\tWindowManager.Instance.Popup(')
    row = replace(row, popup+');', '\t\t\tBindings.PopupEntryDetail(this, spritePath, nameStr, descStr, controlXPos: false);')
    row = replace(row, 'Disposable.AddTo<AssetOperationHandle<Sprite>>(ResourceLoader.LoadSprite(propCfgInfo.SmallIcon), ref mDisposableBag)', 'mDisposableBag.Add(Bindings.Resources.LoadAsset<Sprite>("Assets/Image/" + propCfgInfo.SmallIcon))')
    # Preserve callers of the previously scoped Sprite/string presentation
    # overload while routing the values into the complete original Init path.
    at = row.index('\n\tpublic virtual void Init(')
    compatibility = '''
    public void Init(string propName, string value, Sprite icon, string nextValue = null,
        bool instant = true, bool isBless = false, bool isNegative = false,
        string valueFormatKey = "{0}", bool previewIncreasing = false)
    {
        mFormatKey = valueFormatKey;
        var previousIcon = mGetPropIconFunc;
        mGetPropIconFunc = () => icon;
        try { Init(new ProfessionComplexProp
        {
            PropName = propName, CurValue = 0, NextValue = previewIncreasing ? 1 : 0,
            CurValueObj = ProfessionComplexProp.MakePropValueObj(value),
            NextValueObj = nextValue == null ? null : ProfessionComplexProp.MakePropValueObj(nextValue),
            IsBless = isBless, IsPercentValueNegative = isNegative
        }, instant); }
        finally { mGetPropIconFunc = previousIcon; }
    }
'''
    row = row[:at]+compatibility+row[at:]
    write('ProfessionPlayerPropItem', row, existing=True)
    bless = adapt(read('client-source/BlessPropBtn.cs')).replace('class BlessPropBtn', 'class ProfessionBlessPropBtn')
    at = bless.index('\n{', bless.index('public class '))+2
    bless = bless[:at]+'''
    private IProfessionPropertyBindings mBindings;
    private IProfessionPropertyBindings Bindings => mBindings ?? ProfessionPropertyBindings.Required;
    public void Configure(IProfessionPropertyBindings bindings)
    { mBindings = bindings ?? throw new ArgumentNullException(nameof(bindings)); }
'''+bless[at:]
    bless = bless.replace('PopTipBlessProp.GetBlessDesc', 'ProfessionBlessDescription.GetBlessDesc').replace('CultureNumberArg.Decimal', 'Bindings.DecimalNumber')
    popup = block(bless, '\t\t\tWindowManager.Instance.Popup(')
    bless = replace(bless, popup+');', '\t\t\tBindings.PopupBless(this, mItemRank, mDesc, mEffect, mUnEffectKey, mUnSeasonMatchKey);')
    write('ProfessionBlessPropBtn', bless)
    tip = read('client-source/PopTipBlessProp.cs')
    write('ProfessionBlessDescription', 'public static class ProfessionBlessDescription\n{\nprivate static IProfessionPropertyBindings Bindings => ProfessionPropertyBindings.Required;\n'+adapt(block(tip, '\tpublic static string GetBlessDesc('))+'\n}')
    path = DEST/'ProfessionPropertyBindings.cs'
    meta(path); outputs.extend((record(path), record(path.with_name(path.name+'.meta'))))
    offline = ROOT/'unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionPropertyBindings.cs'
    meta(offline); outputs.extend((record(offline), record(offline.with_name(offline.name+'.meta'))))
    forbidden = ('Parsers.Instance', 'LanguageManager.Instance', 'Formula.Instance', 'WindowManager.Instance', '[Inject]', 'R3;', 'AssetOperationHandle<', 'UnityAction')
    for output in outputs:
        if output['path'].endswith('.cs'):
            text = (ROOT/output['path']).read_text(encoding='utf-8')
            if any(t in text for t in forbidden): raise ValueError('Unbound original dependency: '+output['path'])
    proof = dict(format=1, manifest=checked['manifest'], application_assembly=record(assembly), code_manifest=record(receipt),
        generator=record(Path(__file__)), sources=list({r['path']:r for r in sources}.values()), outputs=outputs,
        reused_language_sources=[record(CODE/'framework-firstpass-source/Framework'/name) for name in ('LanguageType.cs','CultureNumberArg.cs','LanguageCultureResolver.cs')],
        sprite_load_source=record(CODE/'framework-firstpass-source/ResourceLoaderExtensions.cs'),
        operation='Copy every method of original PlayerPropItem, PlayerComplexProp and BlessPropBtn; complete PropType enum; complete consumed IColorfulPropStr, PropValue, PropValueOrig and PropItemValue nested types; original property-format and entry-key methods, six consumed Formula/FormulaHandler number methods, and PopTipBlessProp.GetBlessDesc only. Keep separate main/extra comparison (including original main-vs-extra comparison), formatted-string preview equality, absolute-value and sentinel paths, resource handles until destruction, effects, popup arguments and blessing conditions. Reuse already migrated original culture number formatters; expose parser/localization/resource/popup boundaries explicitly. Retain original row GUID and namespaced enum localization prefix. Keep the previous explicit Sprite/string overload as a caller adapter to original complex values.',
        validated_scope='Pending actual import, simulated input, format and lifetime checks.',
        limitations=['Production property/entry/localization and popup host binding remains incomplete.', 'Only the consumed static blessing description method is migrated here; full blessing popup content/position/rank presentation is still required.', 'Full profession page, save and Windows publication are not claimed.'])
    (ROOT/'assets/reference/profession-ui/property-rows-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Migrated original typed property row, values, formatting and blessing button:',len(outputs)//2,'runtime files')


if __name__ == '__main__':
    build()
