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
    $assembly=[Mono.Cecil.AssemblyDefinition]::CreateAssembly([Mono.Cecil.AssemblyNameDefinition]::new('Framework.Core',[Version]'0.0.0.0'),'Framework.Core',[Mono.Cecil.ModuleKind]::Dll)
    try {
        $module=$assembly.MainModule
        $attribute=[Mono.Cecil.TypeReference]::new('System','Attribute',$module,$module.TypeSystem.Object.Scope)
        $baseCtor=[Mono.Cecil.MethodReference]::new('.ctor',$module.TypeSystem.Void,$attribute);$baseCtor.HasThis=$true
        $portedMarker=[Mono.Cecil.TypeDefinition]::new($marker.Namespace,$marker.Name,$marker.Attributes,$attribute)
        $module.Types.Add($portedMarker)
        $ctor=[Mono.Cecil.MethodDefinition]::new('.ctor',$marker.Methods[0].Attributes,$module.TypeSystem.Void)
        $portedMarker.Methods.Add($ctor);$il=$ctor.Body.GetILProcessor()
        $il.Emit([Mono.Cecil.Cil.OpCodes]::Ldarg_0);$il.Emit([Mono.Cecil.Cil.OpCodes]::Call,$baseCtor);$il.Emit([Mono.Cecil.Cil.OpCodes]::Ret)
        # Keep the runtime usage contract. Dummy RVA/token and JetBrains static
        # analysis annotations are not executable application dependencies.
        $attributeTargets=[Mono.Cecil.TypeReference]::new('System','AttributeTargets',$module,$module.TypeSystem.Object.Scope,$true)
        $usageType=[Mono.Cecil.TypeReference]::new('System','AttributeUsageAttribute',$module,$module.TypeSystem.Object.Scope)
        $usageCtor=[Mono.Cecil.MethodReference]::new('.ctor',$module.TypeSystem.Void,$usageType);$usageCtor.HasThis=$true
        $usageCtor.Parameters.Add([Mono.Cecil.ParameterDefinition]::new($attributeTargets))
        $portedUsage=[Mono.Cecil.CustomAttribute]::new($usageCtor)
        $portedUsage.ConstructorArguments.Add([Mono.Cecil.CustomAttributeArgument]::new($attributeTargets,[int]128))
        foreach($property in $usage.Properties){$portedUsage.Properties.Add([Mono.Cecil.CustomAttributeNamedArgument]::new($property.Name,[Mono.Cecil.CustomAttributeArgument]::new($module.TypeSystem.Boolean,[bool]$property.Argument.Value)))}
        $portedMarker.CustomAttributes.Add($portedUsage)
        $portedInterface=[Mono.Cecil.TypeDefinition]::new($iface.Namespace,$iface.Name,$iface.Attributes)
        $module.Types.Add($portedInterface)
        $interfaceMethod=[Mono.Cecil.MethodDefinition]::new('Inject',$iface.Methods[0].Attributes,$module.TypeSystem.Void)
        $interfaceMethod.Parameters.Add([Mono.Cecil.ParameterDefinition]::new($iface.Methods[0].Parameters[0].Name,$iface.Methods[0].Parameters[0].Attributes,$module.TypeSystem.Object))
        $portedInterface.Methods.Add($interfaceMethod)
        $portedExtension=[Mono.Cecil.TypeDefinition]::new($extension.Namespace,$extension.Name,$extension.Attributes,$module.TypeSystem.Object)
        $module.Types.Add($portedExtension)
        $method=[Mono.Cecil.MethodDefinition]::new('Inject',$inject[0].Attributes,$module.TypeSystem.Void)
        $provider=[Mono.Cecil.TypeReference]::new('System','IServiceProvider',$module,$module.TypeSystem.Object.Scope)
        $method.Parameters.Add([Mono.Cecil.ParameterDefinition]::new($inject[0].Parameters[0].Name,$inject[0].Parameters[0].Attributes,$provider))
        $method.Parameters.Add([Mono.Cecil.ParameterDefinition]::new($inject[0].Parameters[1].Name,$inject[0].Parameters[1].Attributes,$module.TypeSystem.Object))
        $portedExtension.Methods.Add($method)
        $getService=@(($di.MainModule.Types | Where-Object FullName -eq 'Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions').Methods | Where-Object {$_.Name -eq 'GetService' -and $_.HasGenericParameters -and $_.GenericParameters.Count -eq 1 -and $_.Parameters.Count -eq 1})
        if($getService.Count -ne 1){throw 'Pinned public GetService<T> ABI differs'}
        $generic=[Mono.Cecil.GenericInstanceMethod]::new($module.ImportReference($getService[0]));$generic.GenericArguments.Add($portedInterface)
        $il=$method.Body.GetILProcessor()
        $il.Emit([Mono.Cecil.Cil.OpCodes]::Ldarg_0);$il.Emit([Mono.Cecil.Cil.OpCodes]::Call,$generic)
        $il.Emit([Mono.Cecil.Cil.OpCodes]::Ldarg_1);$il.Emit([Mono.Cecil.Cil.OpCodes]::Callvirt,$interfaceMethod);$il.Emit([Mono.Cecil.Cil.OpCodes]::Ret)
        $extensionType=[Mono.Cecil.TypeReference]::new('System.Runtime.CompilerServices','ExtensionAttribute',$module,$module.TypeSystem.Object.Scope)
        $extensionCtor=[Mono.Cecil.MethodReference]::new('.ctor',$module.TypeSystem.Void,$extensionType);$extensionCtor.HasThis=$true
        $portedExtension.CustomAttributes.Add([Mono.Cecil.CustomAttribute]::new($extensionCtor));$method.CustomAttributes.Add([Mono.Cecil.CustomAttribute]::new($extensionCtor))
        $injector=$original.MainModule.Types | Where-Object FullName -eq 'Framework.DependencyInjection.DryIoc.DryIocInstanceInjector'
        if($injector.Fields.Count -ne 1 -or $injector.Methods.Count -ne 2 -or $injector.Fields[0].Name -ne 'mContext' -or $injector.Fields[0].FieldType.FullName -ne 'DryIoc.IResolverContext' -or $injector.BaseType.FullName -ne 'System.Object' -or $injector.Interfaces.Count -ne 1 -or $injector.Interfaces[0].InterfaceType.FullName -ne 'Framework.DependencyInjection.Abstractions.IInstanceInjector'){throw 'Original injector layout differs'}
        $originalCtor=$injector.Methods | Where-Object Name -eq '.ctor'
        $originalInject=$injector.Methods | Where-Object Name -eq 'Inject'
        if($originalCtor.FullName -ne 'System.Void Framework.DependencyInjection.DryIoc.DryIocInstanceInjector::.ctor(DryIoc.IResolverContext)' -or $originalInject.FullName -ne 'System.Void Framework.DependencyInjection.DryIoc.DryIocInstanceInjector::Inject(System.Object)'){throw 'Original injector method ABI differs'}
        $context=$dry.MainModule.Types | Where-Object FullName -eq 'DryIoc.IResolverContext'
        $portedInjector=[Mono.Cecil.TypeDefinition]::new($injector.Namespace,$injector.Name,$injector.Attributes,$module.TypeSystem.Object)
        $portedInjector.Interfaces.Add([Mono.Cecil.InterfaceImplementation]::new($portedInterface));$module.Types.Add($portedInjector)
        $field=[Mono.Cecil.FieldDefinition]::new('mContext',$injector.Fields[0].Attributes,$module.ImportReference($context));$portedInjector.Fields.Add($field)
        $injectorCtor=[Mono.Cecil.MethodDefinition]::new('.ctor',$originalCtor.Attributes,$module.TypeSystem.Void)
        $injectorCtor.Parameters.Add([Mono.Cecil.ParameterDefinition]::new($originalCtor.Parameters[0].Name,$originalCtor.Parameters[0].Attributes,$module.ImportReference($context)))
        $portedInjector.Methods.Add($injectorCtor);$il=$injectorCtor.Body.GetILProcessor()
        $objectCtor=[Mono.Cecil.MethodReference]::new('.ctor',$module.TypeSystem.Void,$module.TypeSystem.Object);$objectCtor.HasThis=$true
        $il.Emit([Mono.Cecil.Cil.OpCodes]::Ldarg_0);$il.Emit([Mono.Cecil.Cil.OpCodes]::Call,$objectCtor)
        $il.Emit([Mono.Cecil.Cil.OpCodes]::Ldarg_0);$il.Emit([Mono.Cecil.Cil.OpCodes]::Ldarg_1);$il.Emit([Mono.Cecil.Cil.OpCodes]::Stfld,$field);$il.Emit([Mono.Cecil.Cil.OpCodes]::Ret)
        $injectorMethod=[Mono.Cecil.MethodDefinition]::new('Inject',$originalInject.Attributes,$module.TypeSystem.Void)
        $injectorMethod.Parameters.Add([Mono.Cecil.ParameterDefinition]::new($originalInject.Parameters[0].Name,$originalInject.Parameters[0].Attributes,$module.TypeSystem.Object));$portedInjector.Methods.Add($injectorMethod)
        $targets=@(($dry.MainModule.Types | Where-Object FullName -eq 'DryIoc.ContainerTools').Methods | Where-Object {$_.Name -eq 'InjectPropertiesAndFields' -and $_.GenericParameters.Count -eq 1 -and $_.Parameters.Count -eq 2})
        if($targets.Count -ne 1){throw 'Pinned original DryIoc injection call differs'}
        $genericInject=[Mono.Cecil.GenericInstanceMethod]::new($module.ImportReference($targets[0]));$genericInject.GenericArguments.Add($module.TypeSystem.Object)
        $il=$injectorMethod.Body.GetILProcessor();$il.Emit([Mono.Cecil.Cil.OpCodes]::Ldarg_0);$il.Emit([Mono.Cecil.Cil.OpCodes]::Ldfld,$field)
        $il.Emit([Mono.Cecil.Cil.OpCodes]::Ldarg_1);$il.Emit([Mono.Cecil.Cil.OpCodes]::Call,$genericInject);$il.Emit([Mono.Cecil.Cil.OpCodes]::Pop);$il.Emit([Mono.Cecil.Cil.OpCodes]::Ret)
        $identity=[System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($IdentitySeed))
        $module.Mvid=[guid]::new([byte[]]$identity[0..15])
        New-Item -ItemType Directory -Path $Destination -Force | Out-Null
        $writer=[Mono.Cecil.WriterParameters]::new();$writer.Timestamp=[uint32]0
        $assembly.Write((Join-Path $Destination 'Framework.Core.dll'),$writer)
        @{name='Framework.Core';module_id=$module.Mvid.ToString();types=@($portedMarker.FullName,$portedInterface.FullName,$portedExtension.FullName,$portedInjector.FullName);method=$method.FullName;injector=$injectorMethod.FullName;injector_target=$targets[0].FullName} | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $Destination 'contract.json') -Encoding utf8
    } finally {$assembly.Dispose()}
} finally {$original.Dispose();$di.Dispose();$dry.Dispose();$framework.Dispose()}
