param(
    [Parameter(Mandatory)][string]$OriginalCore,
    [Parameter(Mandatory)][string]$OriginalLaunch,
    [Parameter(Mandatory)][string]$DependencyInjectionAbstractions,
    [Parameter(Mandatory)][string]$DryIoc,
    [Parameter(Mandatory)][string]$OriginalFramework,
    [Parameter(Mandatory)][string]$Destination,
    [Parameter(Mandatory)][string]$IdentitySeed,
    [string]$Cecil='D:/Tool/Unity/Editor/Data/Tools/ScriptUpdater/Unity.Cecil.dll'
)
$ErrorActionPreference='Stop'
Add-Type -Path $Cecil
$launch=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($OriginalLaunch)
try {
    $version=$launch.MainModule.Types | Where-Object FullName -eq 'IVersionManager'
    $expected=@('System.String IVersionManager::get_Version()','System.Int32 IVersionManager::get_ResVersion()','System.Void IVersionManager::set_ResVersion(System.Int32)','System.Int32 IVersionManager::get_CodeVersion()','System.Void IVersionManager::set_CodeVersion(System.Int32)','System.Void IVersionManager::Reload()')
    if(!$version.IsInterface -or $version.Fields.Count -ne 0 -or $version.Interfaces.Count -ne 0 -or (@($version.Methods.FullName) -join '|') -ne ($expected -join '|')){throw 'Original Launch.IVersionManager ABI differs'}
} finally {$launch.Dispose()}
$original=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($OriginalCore)
$di=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($DependencyInjectionAbstractions)
$dry=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($DryIoc)
$framework=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($OriginalFramework)
try {
    $reference=$framework.MainModule.AssemblyReferences | Where-Object Name -eq 'DryIoc'
    if($reference.FullName -ne $dry.Name.FullName -or $dry.Name.FullName -ne 'DryIoc, Version=5.4.3.0, Culture=neutral, PublicKeyToken=dfbf2bd50fcf7768'){throw 'Original client DryIoc assembly identity differs'}
    $marker=$original.MainModule.Types | Where-Object FullName -eq 'Framework.DependencyInjection.InjectAttribute'
    $iface=$original.MainModule.Types | Where-Object FullName -eq 'Framework.DependencyInjection.Abstractions.IInstanceInjector'
    $extension=$original.MainModule.Types | Where-Object FullName -eq 'Framework.DependencyInjection.ServiceProviderExtensions'
    $inject=@($extension.Methods | Where-Object Name -eq 'Inject')
    if($marker.Fields.Count -ne 0 -or $marker.Methods.Count -ne 1 -or $iface.Fields.Count -ne 0 -or $iface.Methods.Count -ne 1 -or $inject.Count -ne 1){throw 'Original dependency injection ABI differs'}
    if($iface.Methods[0].FullName -ne 'System.Void Framework.DependencyInjection.Abstractions.IInstanceInjector::Inject(System.Object)' -or $inject[0].FullName -ne 'System.Void Framework.DependencyInjection.ServiceProviderExtensions::Inject(System.IServiceProvider,System.Object)'){throw 'Original injection method parameters differ'}
    $usage=$marker.CustomAttributes | Where-Object {$_.AttributeType.FullName -eq 'System.AttributeUsageAttribute'}
    if($usage.ConstructorArguments[0].Value -ne 128 -or @($usage.Properties | Where-Object {$_.Name -eq 'AllowMultiple' -and $_.Argument.Value -eq $false}).Count -ne 1 -or @($usage.Properties | Where-Object {$_.Name -eq 'Inherited' -and $_.Argument.Value -eq $true}).Count -ne 1){throw 'Original injection attribute usage differs'}
    # Compile only the recovered injection subset against the Editor's .NET Standard
    # contract. No DummyDll method body is copied into the resulting assembly.
    $source=Join-Path $PSScriptRoot 'fixtures/mainline_shared_injection.cs'
    $editorData='D:/Tool/Unity/Editor/Data'
    $references=@(Get-ChildItem (Join-Path $editorData 'NetStandard/ref/2.1.0') -Filter '*.dll')
    if($references.Count -eq 0){throw 'Unity .NET Standard reference assemblies are unavailable'}
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    $compiled=Join-Path $Destination 'compiled'
    New-Item -ItemType Directory -Path $compiled -Force | Out-Null
    $output=Join-Path $compiled 'Framework.Core.dll'
    $arguments=@('-noconfig','-nostdlib+','-target:library','-optimize+','-deterministic+',('-out:'+$output))
    $arguments+=@($references | ForEach-Object {'-r:'+$_.FullName})
    $arguments+=@(('-r:'+$DryIoc),('-r:'+$DependencyInjectionAbstractions),$source)
    & (Join-Path $editorData 'NetCoreRuntime/dotnet.exe') (Join-Path $editorData 'DotNetSdkRoslyn/csc.dll') @arguments
    if($LASTEXITCODE -ne 0){throw 'Original injection subset compilation failed'}
    Copy-Item -LiteralPath $DryIoc -Destination (Join-Path $compiled 'DryIoc.dll') -Force
    Copy-Item -LiteralPath $DependencyInjectionAbstractions -Destination (Join-Path $compiled 'Microsoft.Extensions.DependencyInjection.Abstractions.dll') -Force
    $resolver=[Mono.Cecil.DefaultAssemblyResolver]::new()
    $resolver.AddSearchDirectory($compiled)
    $resolver.AddSearchDirectory((Join-Path $editorData 'NetStandard/ref/2.1.0'))
    $reader=[Mono.Cecil.ReaderParameters]::new();$reader.AssemblyResolver=$resolver
    $assembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($output,$reader)
    try {
        $module=$assembly.MainModule
        function Assert-OriginalType($ported,$expected) {
            if(!$expected -or $ported.Attributes -ne $expected.Attributes -or $ported.BaseType.FullName -ne $expected.BaseType.FullName){throw ('Original type ABI differs: '+$ported.FullName)}
            if(($ported.Interfaces.InterfaceType.FullName -join '|') -ne ($expected.Interfaces.InterfaceType.FullName -join '|')){throw ('Original interfaces differ: '+$ported.FullName)}
            if($ported.Fields.Count -ne $expected.Fields.Count){throw ('Original fields differ: '+$ported.FullName)}
            foreach($field in $ported.Fields){
                $matches=@($expected.Fields | Where-Object {$_.FullName -eq $field.FullName -and $_.Attributes -eq $field.Attributes})
                if($matches.Count -ne 1){throw ('Original field ABI differs: '+$field.FullName)}
            }
            foreach($method in $ported.Methods){
                $matches=@($expected.Methods | Where-Object {$_.FullName -eq $method.FullName -and $_.Attributes -eq $method.Attributes})
                if($matches.Count -ne 1){throw ('Original method ABI differs: '+$method.FullName)}
            }
            if($ported.Name -ne 'ServiceProviderExtensions' -and $ported.Methods.Count -ne $expected.Methods.Count){throw ('Original method count differs: '+$ported.FullName)}
            if($ported.Name -ne 'ServiceProviderExtensions' -and $ported.NestedTypes.Count -ne $expected.NestedTypes.Count){throw ('Original nested types differ: '+$ported.FullName)}
            foreach($nested in $ported.NestedTypes){Assert-OriginalType $nested ($expected.NestedTypes | Where-Object FullName -eq $nested.FullName)}
        }
        $types=@($module.Types | Where-Object Name -ne '<Module>')
        $names=@('Framework.DependencyInjection.InjectAttribute','Framework.DependencyInjection.Abstractions.IInstanceInjector','Framework.DependencyInjection.ServiceProviderExtensions','Framework.DependencyInjection.DryIoc.DryIocInstanceInjector','Framework.DependencyInjection.DryIoc.InjectAttributeSelector')
        if((@($types.FullName | Sort-Object) -join '|') -ne (@($names | Sort-Object) -join '|')){throw 'Unexpected partial Core type set'}
        foreach($type in $types){Assert-OriginalType $type ($original.MainModule.Types | Where-Object FullName -eq $type.FullName)}
        $selector=$types | Where-Object Name -eq 'InjectAttributeSelector'
        $properties=$selector.Methods | Where-Object Name -eq 'Properties'
        if($properties.Parameters.Count -ne 1 -or !$properties.Parameters[0].HasConstant -or [int]$properties.Parameters[0].Constant -ne 2){throw 'Original selector default differs'}
        $identity=[System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($IdentitySeed+(Get-FileHash -Algorithm SHA256 $source).Hash))
        $module.Mvid=[guid]::new([byte[]]$identity[0..15])
        $writer=[Mono.Cecil.WriterParameters]::new();$writer.Timestamp=[uint32]0
        $assembly.Write((Join-Path $Destination 'Framework.Core.dll'),$writer)
        @{name='Framework.Core';module_id=$module.Mvid.ToString();types=$names;method='System.Void Framework.DependencyInjection.ServiceProviderExtensions::Inject(System.IServiceProvider,System.Object)';injector='System.Void Framework.DependencyInjection.DryIoc.DryIocInstanceInjector::Inject(System.Object)';injector_target='DryIoc.ContainerTools.InjectPropertiesAndFields<System.Object>';selector=$properties.FullName} | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $Destination 'contract.json') -Encoding utf8
    } finally {$assembly.Dispose();$resolver.Dispose()}
} finally {$original.Dispose();$di.Dispose();$dry.Dispose();$framework.Dispose()}
