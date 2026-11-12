"""Migrate the selected global-pointer operation and original input dispatcher."""
import hashlib
import json
import re
from pathlib import Path

from prepare_home_auto_increase_runtime import ROOT, CODE, metadata, sha, method
from prepare_partner_gift_configuration import normalize


def build():
    inputs, outputs = {}, {}
    for name in ('prepare_home_auto_increase_runtime.py', 'prepare_partner_gift_configuration.py'):
        path = ROOT/'scripts'/name
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)

    def source(name):
        path = CODE/'client-source'/(name+'.cs')
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
        return path.read_text('utf-8-sig').replace('\r\n', '\n')

    def strip(text):
        text = re.sub(r'^using [^\n]*;\n|^\s*//IL_[^\n]*\n', '', text, flags=re.M)
        return text.strip()+'\n'

    text = '''using System;
using System.Collections.Generic;
using UnityEngine;
namespace Zjcs.Official.HomeMainUiControls
{
    // OS input is read only by the desktop owner. Isolated validation supplies
    // process-local samples through this same boundary, without system input.
    public interface IHomePointerSnapshot
    {
        bool LeftDown { get; }
        Vector2 Position { get; }
        int Width { get; }
        int Height { get; }
    }
'''
    for name in ('IInputOperationResult', 'IInputOperation', 'IInputOperationModule',
                 'InputOperationLayer', 'InputOperationCallbackInfo'):
        text += strip(source(name))
    base = source('InputOperationBase')
    call = method(base, 'public void Call(Delegate callback)')
    start = call.index('\n\t\t\tGLogger')
    end = call.index('\n\t\t}', start)
    call = call[:start]+'\n\t\t\treportException(exception);'+call[end:]
    base = base.replace(method(base, 'public void Call(Delegate callback)'), call)
    base = base.replace('public InputOperationBase()', '''protected readonly IHomePointerSnapshot pointer;
    private readonly Action<Exception> reportException;
    public InputOperationBase(IHomePointerSnapshot pointer, Action<Exception> reportException)''')
    base = base.replace('LayerHashCode = Layer.GetHashCode();', '''this.pointer = pointer ?? throw new ArgumentNullException(nameof(pointer));
        this.reportException = reportException ?? throw new ArgumentNullException(nameof(reportException));
        LayerHashCode = Layer.GetHashCode();''')
    base = base.replace('Screen.width', 'pointer.Width').replace('Screen.height', 'pointer.Height')
    text += strip(base)
    operation = source('GlobalPointerDownOperation')
    operation = operation.replace('public override InputOperationLayer Layer', '''public GlobalPointerDownOperation(IHomePointerSnapshot pointer, Action<Exception> reportException)
        : base(pointer, reportException) { }
    public override InputOperationLayer Layer''')
    operation = operation.replace('Input.GetMouseButtonDown(0)', 'pointer.LeftDown')
    operation = operation.replace('Vector2.op_Implicit(Input.mousePosition)', 'pointer.Position')
    text += strip(operation)
    result = source('OnGlobalPointerDownResult')
    start = result.index('\tpublic Vector2 ScreenPos')
    end = result.index('\n\tpublic OnGlobalPointerDownResult', start)
    result = result[:start]+'\tpublic Vector2 ScreenPos { get; private set; }\n'+result[end:]
    result = result.replace('Vector2.op_Implicit(screenPos)', '(Vector2)screenPos')
    result = result.replace('Vector2.op_Implicit(result.ScreenPos)', '(Vector3)result.ScreenPos')
    text += strip(result)
    module = source('InputOperationModule')
    module = module.replace(': BackgroundServiceBase, IInputOperationModule, IUpdatable', ': IInputOperationModule')
    module = module.replace('private IServiceProvider mServiceProvider;', 'private readonly Action<string> reportDuplicate;')
    module = module.replace('public InputOperationModule(IServiceProvider serviceProvider)',
        'public InputOperationModule(IEnumerable<IInputOperation> operations, Action<string> reportDuplicate)')
    module = module.replace('mServiceProvider = serviceProvider;',
        'this.reportDuplicate = reportDuplicate ?? throw new ArgumentNullException(nameof(reportDuplicate));')
    module = module.replace('LoadOperations();', 'LoadOperations(operations);')
    old = method(module, 'private void LoadOperations()')
    module = module.replace(old, '''private void LoadOperations(IEnumerable<IInputOperation> operations)
    {
        if (operations == null) throw new ArgumentNullException(nameof(operations));
        foreach (IInputOperation inputOperation in operations)
        {
            mInputOperations.Add(inputOperation);
            mCallbackExecutionBuffer.Add(inputOperation.ResultType, new List<long>());
            mBlockSignatures.Add(inputOperation.ResultType.GetHashCode(), new HashSet<long>());
        }
    }
''')
    # Static reflection/DI discovery is replaced by the explicit selected set.
    # Registry, reverse dispatch, lazy removal and both blocking domains remain.
    module = module.replace('void IUpdatable.Update()', 'public void Update()')
    module, errors = re.subn(r'\t+GLoggerErrorInterpolatedStringHandler message =[^\n]*\n.*?\n\t+Log.Error\([^\n]*\);',
        '\t\t\treportDuplicate(callback + " 被重复Hook!");', module, flags=re.S)
    module, infos = re.subn(r'\t+GLoggerInfoInterpolatedStringHandler message =[^\n]*\n.*?\n\t+Log.Info\([^\n]*\);\n', '', module, flags=re.S)
    assert (errors, infos) == (1, 2)
    module = module.replace('mSignaturesMatcher.Remove((Delegate)callback, ref key)',
        'mSignaturesMatcher.Remove((Delegate)callback, out key)')
    text += strip(module)+'}\n'
    assert not any(value in text for value in ('GLogger', 'Log.Error', 'Log.Info', 'Vector2.op_',
        'field;', 'Framework.', 'ReflectionClassManager', 'mServiceProvider', 'CompilerGenerated'))
    path = ROOT/'unity/Assets/Game/Runtime/Official/HomeMainUiControls/HomePointerInput.cs'
    path.write_text(normalize('// Selected original input dispatcher and global-pointer operation.\n'+text), 'utf8', newline='\n')
    for item in (path, metadata(path)):
        outputs[item.relative_to(ROOT).as_posix()] = sha(item)
    receipt = dict(manifest='884_156692', generator_sha256=sha(Path(__file__)), inputs=inputs, outputs=outputs,
        adaptations=[
            'Original callback signatures, reverse dispatch, lazy unhook removal, callback exception isolation and layer/type block tokens.',
            'Reflection/DI operation discovery replaced by explicit selected operation instances, preserving their supplied order.',
            'Pointer and screen reads supplied by the desktop owner; original inclusive screen test and clamping retained.',
            'Original diagnostics delivered through mandatory exception/duplicate-hook reporters; informational logs omitted.',
            'Decompiler backing-field, vector conversion and Dictionary.Remove syntax repaired for the project C# runtime.'])
    (ROOT/'assets/reference/home-pointer-input-source-manifest.json').write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
    print('Original home input dispatcher:', len(inputs)-2, 'selected sources', flush=True)


if __name__ == '__main__':
    build()
