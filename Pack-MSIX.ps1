#Requires -Version 5.1
<#
.SYNOPSIS
    Genera el paquete MSIX de QuickPreview listo para subir a la Microsoft Store.

.PREREQUISITES
    1. Haber ejecutado GenerateAssets.ps1 al menos una vez.
    2. Haber rellenado los campos TODO en Package\AppxManifest.xml.
    3. Windows SDK instalado (para makeappx.exe).

.USAGE
    .\Pack-MSIX.ps1

    Genera: QuickPreview_1.0.0.0_x64.msix
#>

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot

# ── 1. Buscar makeappx.exe (Windows SDK o Visual Studio o NuGet) ───────────────
function Find-MakeAppx {
    # Windows SDK
    foreach ($root in @("C:\Program Files (x86)\Windows Kits\10\bin","C:\Program Files\Windows Kits\10\bin")) {
        $exe = Get-ChildItem "$root\*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
               Sort-Object { [version]($_.Directory.Parent.Name) } -Descending |
               Select-Object -First 1 -ExpandProperty FullName
        if ($exe) { return $exe }
    }
    # Visual Studio
    $exe = Get-ChildItem "C:\Program Files\Microsoft Visual Studio\*\*\MSBuild\Microsoft\VisualStudio\*\AppxPackage\makeappx.exe" -ErrorAction SilentlyContinue |
           Select-Object -First 1 -ExpandProperty FullName
    if ($exe) { return $exe }
    # NuGet cache (Microsoft.Windows.SDK.BuildTools)
    $exe = Get-ChildItem "$env:USERPROFILE\.nuget\packages\microsoft.windows.sdk.buildtools\*\bin\*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
           Select-Object -First 1 -ExpandProperty FullName
    return $exe
}

$makeappx = Find-MakeAppx
if (-not $makeappx) {
    Write-Host "makeappx.exe no encontrado. Descargando Microsoft.Windows.SDK.BuildTools via NuGet (~6MB)..."
    $tmpDir = Join-Path $env:TEMP "qp-sdk-tools"
    New-Item -ItemType Directory -Force $tmpDir | Out-Null
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0-windows</TargetFramework></PropertyGroup>
  <ItemGroup><PackageReference Include="Microsoft.Windows.SDK.BuildTools" Version="10.0.26100.1742" /></ItemGroup>
</Project>
"@ | Set-Content "$tmpDir\tools.csproj"
    & dotnet restore "$tmpDir\tools.csproj" | Out-Null
    Remove-Item $tmpDir -Recurse -Force
    $makeappx = Find-MakeAppx
    if (-not $makeappx) { Write-Error "No se pudo obtener makeappx.exe. Instala el Windows SDK manualmente." }
}
Write-Host "makeappx: $makeappx"

# ── 2. Verificar que AppxManifest no tenga TODOs sin rellenar ─────────────────
$manifest = Get-Content "$ProjectRoot\Package\AppxManifest.xml" -Raw
if ($manifest -match "TODO_") {
    Write-Error @"
Package\AppxManifest.xml tiene campos sin completar (marcados con TODO_).
Rellena Name, Publisher y PublisherDisplayName con los datos de tu cuenta Partner Center.
"@
}

# ── 3. Publicar la app (self-contained, sin runtime externo) ───────────────────
$publishDir = "$ProjectRoot\publish-msix"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

Write-Host "`nPublicando app (self-contained)..."
& dotnet publish "$ProjectRoot\QuickPreview\QuickPreview.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o "$publishDir" `
    -p:PublishReadyToRun=true
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# ── 4. Preparar carpeta de staging ────────────────────────────────────────────
$stagingDir = "$ProjectRoot\staging-msix"
if (Test-Path $stagingDir) { Remove-Item $stagingDir -Recurse -Force }
New-Item -ItemType Directory $stagingDir | Out-Null

Write-Host "`nCopiando archivos al staging..."
Copy-Item "$publishDir\*" "$stagingDir\" -Recurse
Copy-Item "$ProjectRoot\Package\AppxManifest.xml" "$stagingDir\"
Copy-Item "$ProjectRoot\Package\Assets" "$stagingDir\Assets" -Recurse

# ── 5. Empaquetar ─────────────────────────────────────────────────────────────
$version = ([xml](Get-Content "$stagingDir\AppxManifest.xml")).Package.Identity.Version
$outputMsix = "$ProjectRoot\QuickPreview_${version}_x64.msix"
if (Test-Path $outputMsix) { Remove-Item $outputMsix -Force }

Write-Host "`nEmpaquetando MSIX..."
& $makeappx pack /d "$stagingDir" /p "$outputMsix" /nv
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# ── 6. Limpiar staging ────────────────────────────────────────────────────────
Remove-Item $stagingDir -Recurse -Force

Write-Host "`n✓ Paquete MSIX generado: $outputMsix"
Write-Host "  Sube este archivo a Partner Center → tu app → Paquetes → Agregar paquete`n"
