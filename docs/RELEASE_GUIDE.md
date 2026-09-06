# Guía de publicación de Audio Switcher

Esta guía describe el proceso completo para subir una nueva versión de Audio
Switcher, generar el paquete `.pext`, publicar los cambios en GitHub y verificar
la release.

Todos los textos públicos de la release, incluido el changelog de
`installer.yaml`, deben escribirse en inglés.

## Método recomendado: script automatizado

La forma más rápida y segura es usar `release.ps1`. El script actualiza las
versiones, genera o actualiza `CHANGELOG.md` e `installer.yaml`, ejecuta todas
las pruebas disponibles, crea el `.pext` y verifica su contenido y hash.

Primero crea `.release-notes.md` con una línea por cambio, siempre en inglés:

```markdown
- Added the main new feature.
- Fixed the relevant bug.
- Improved Playnite compatibility.
```

El fichero está ignorado por Git y se reutiliza para el changelog, el instalador
y las notas de GitHub.

Preparar y validar sin publicar nada:

```powershell
.\release.ps1 -Version 1.17.2
```

La primera ejecución crea una plantilla de `.release-notes.md` si todavía no
existe. Tras editarla, vuelve a ejecutar el comando.

Cuando hayas revisado el diff, publica la release completa:

```powershell
.\release.ps1 -Version 1.17.2 -Publish
```

Antes del commit y la publicación exige escribir `RELEASE 1.17.2`. Para una
ejecución no interactiva deliberada puede usarse `-Yes`:

```powershell
.\release.ps1 -Version 1.17.2 -Publish -Yes
```

Las secciones siguientes documentan el procedimiento manual equivalente y
sirven también para diagnosticar cualquier fallo del script.

## 1. Abrir el repositorio

```powershell
cd C:\Proyectos\playnite-nx-audio-switcher
```

## 2. Comprobar los requisitos y sincronizar referencias

```powershell
gh auth status
Test-Path C:\Playnite\Toolbox.exe
git fetch --tags origin
gh release list --repo Naerian/playnite-nx-audio-switcher --limit 5
git status -sb
```

`Test-Path` debe devolver `True`. Antes de continuar, comprueba que `main`
coincida con `origin/main` y revisa cualquier cambio local. No mezcles en la
release archivos o modificaciones ajenos.

Si GitHub CLI todavía no está autenticado:

```powershell
gh auth login
```

## 3. Elegir y configurar la versión

Ejemplo para una actualización de parche posterior a `1.17.1`:

```powershell
$Version = "1.17.2"
$Tag = "v$Version"
$VersionForFile = $Version -replace '\.', '_'
$ReleaseDate = Get-Date -Format 'yyyy-MM-dd'
```

Actualiza estos cuatro ficheros:

1. `extension.yaml`:

   ```yaml
   Version: 1.17.2
   ```

2. `PlayniteAudioSwitcher.csproj`:

   ```xml
   <Version>1.17.2</Version>
   <AssemblyVersion>1.17.2.0</AssemblyVersion>
   <FileVersion>1.17.2.0</FileVersion>
   ```

3. Añade una sección nueva al principio de `CHANGELOG.md`:

   ```markdown
   ## 1.17.2 — AAAA-MM-DD

   - Describe the main new feature or fix.
   - Describe other relevant changes.
   ```

4. Añade el nuevo paquete al principio de `installer.yaml`, sin eliminar las
   versiones anteriores:

   ```yaml
   - Version: 1.17.2
     RequiredApiVersion: 6.16.0
     ReleaseDate: AAAA-MM-DD
     PackageUrl: https://github.com/Naerian/playnite-nx-audio-switcher/releases/download/v1.17.2/PlayniteAudioSwitcher_708b6ec4-bf96-4c0d-bd9d-fe0aa04d6bf1_1_17_2.pext
     Changelog:
       - "Describe the main new feature or fix."
       - "Describe other relevant changes."
   ```

El nombre del asset sigue este patrón:

```text
PlayniteAudioSwitcher_708b6ec4-bf96-4c0d-bd9d-fe0aa04d6bf1_<versión_con_guiones_bajos>.pext
```

Comprueba que no queden referencias accidentales a la versión anterior en los
ficheros de versión activos. Las entradas históricas de `installer.yaml` deben
conservarla:

```powershell
Select-String -Path extension.yaml,PlayniteAudioSwitcher.csproj -Pattern '1\.17\.1'
Get-Content installer.yaml -TotalCount 20
```

## 4. Compilar y generar el paquete `.pext`

```powershell
.\package.ps1
```

El script:

- Lee y valida la versión de `extension.yaml`.
- Ejecuta `dotnet restore`, `dotnet clean` y `dotnet build` en Release.
- Comprueba los componentes obligatorios de la salida.
- Prepara una carpeta temporal limpia.
- Ejecuta `C:\Playnite\Toolbox.exe pack`.
- Genera el paquete en `dist\<versión>\` y muestra su SHA-256.

No hay actualmente una suite de tests automatizados separada en este
repositorio. La compilación Release sin errores ni advertencias y la generación
correcta del paquete son las comprobaciones automatizadas obligatorias. Para
cambios de comportamiento, realiza además la comprobación manual relevante en
Playnite antes de publicar.

Guarda la ruta y el hash del paquete:

```powershell
$Package = Get-ChildItem -LiteralPath "dist\$Version" -Filter '*.pext' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
$LocalHash = (Get-FileHash -LiteralPath $Package.FullName -Algorithm SHA256).Hash

$Package.FullName
$Package.Length
$LocalHash
```

Comprueba que el paquete contiene los componentes esenciales:

```powershell
tar -tf $Package.FullName |
    Select-String 'extension.yaml|PlayniteAudioSwitcher.dll|README.md|Localization/|Icons/|media/icon.png|Examples/'
```

## 5. Revisar los cambios

```powershell
git diff --check
git diff --stat
git status --short
```

Revisa también el diff completo:

```powershell
git diff
```

No continúes si aparecen archivos privados, diagnósticos, configuraciones
locales, logs, secretos, artefactos no ignorados o cambios que no pertenecen a
la release. La carpeta `dist` está ignorada y el `.pext` no debe incluirse en el
commit.

## 6. Crear el commit y subir `main`

Añade únicamente los ficheros revisados. Incluye siempre los cuatro ficheros de
versión y los cambios que forman parte de la release:

```powershell
$ReleaseFiles = @(
    'extension.yaml'
    'PlayniteAudioSwitcher.csproj'
    'CHANGELOG.md'
    'installer.yaml'
    # Añade aquí cada fichero de implementación o documentación revisado.
    'AudioSwitcherPlugin.cs'
)
git add -- $ReleaseFiles
git diff --cached --check
git diff --cached --stat
git commit -m "Release Audio Switcher $Version"
git push origin main
```

Comprueba que el commit local y el remoto coinciden:

```powershell
git status -sb
git log -1 --oneline --decorate
```

## 7. Crear y subir el tag

Audio Switcher usa tags anotados con el prefijo `v`:

```powershell
git tag -a $Tag -m "Audio Switcher $Tag"
git push origin $Tag
git rev-list -n 1 $Tag
```

El tag debe apuntar al mismo commit que `HEAD`. No publiques la release si el
tag apunta a otro commit.

## 8. Preparar las notas de GitHub

Las notas deben estar en inglés y resumir el cambio para los usuarios. Incluye
la verificación realizada y el hash del paquete:

```powershell
$NotesFile = Join-Path $env:TEMP "audio-switcher-$Version-release-notes.md"

@"
## What's Changed

- Describe the main user-visible change.
- Describe important fixes or compatibility improvements.

## Verification

- Release build completed with no errors or warnings.
- The Playnite package was generated successfully.
- Describe the relevant manual Playnite check, when applicable.

SHA-256: ``$LocalHash``
"@ | Set-Content -LiteralPath $NotesFile -Encoding UTF8

Get-Content -LiteralPath $NotesFile
```

## 9. Crear la release

```powershell
gh release create $Tag $Package.FullName `
    --repo Naerian/playnite-nx-audio-switcher `
    --title "Audio Switcher $Tag" `
    --notes-file $NotesFile `
    --latest `
    --verify-tag
```

`--verify-tag` evita publicar sobre un tag inexistente o distinto del que se
acaba de subir. Cuando termine:

```powershell
Remove-Item -LiteralPath $NotesFile
```

## 10. Verificar la release pública

Consulta la release y el asset publicado:

```powershell
$Published = gh release view $Tag `
    --repo Naerian/playnite-nx-audio-switcher `
    --json url,name,isDraft,isPrerelease,tagName,assets,body,publishedAt |
    ConvertFrom-Json

$Published.url
$Published.isDraft
$Published.isPrerelease
$Published.assets
```

La release debe tener:

- `isDraft` igual a `False`.
- `isPrerelease` igual a `False`, salvo que sea una beta deliberada.
- El tag, título y notas esperados.
- Un único `.pext` con el nombre y la versión correctos.

Compara el hash local con el asset publicado:

```powershell
$RemoteAsset = $Published.assets |
    Where-Object { $_.name -eq $Package.Name } |
    Select-Object -First 1

if (-not $RemoteAsset) {
    throw "The expected .pext asset was not published."
}

$RemoteHash = $RemoteAsset.digest -replace '^sha256:', ''
if ($LocalHash -ne $RemoteHash.ToUpperInvariant()) {
    throw "The public asset hash does not match the local package."
}

"Public asset hash verified: $LocalHash"
```

## 11. Verificar el instalador público

Usa un parámetro en la URL para evitar una respuesta antigua de la caché:

```powershell
$InstallerUrl = "https://raw.githubusercontent.com/Naerian/playnite-nx-audio-switcher/main/installer.yaml?release=$Version"
$PublicInstaller = (Invoke-WebRequest -UseBasicParsing -Uri $InstallerUrl).Content
$PublicInstaller | Select-String -Pattern "Version: $Version|PackageUrl:"
```

La primera entrada debe ser la nueva versión y su `PackageUrl` debe coincidir
exactamente con el asset publicado.

## 12. Comprobación final

```powershell
git fetch --tags origin
git status -sb
git log -1 --oneline --decorate
git tag --points-at HEAD
git rev-list -n 1 $Tag
```

La publicación está cerrada correctamente cuando:

- El tag aparece sobre el commit esperado.
- `main` coincide con `origin/main`.
- `git status --short` no devuelve cambios.
- La release no es borrador ni prerelease accidental.
- El `.pext` público tiene el mismo SHA-256 que el paquete local.
- El `installer.yaml` público anuncia la versión nueva.

## Resumen rápido

Después de actualizar `extension.yaml`, `PlayniteAudioSwitcher.csproj`,
`CHANGELOG.md` e `installer.yaml`, el flujo mínimo es:

```powershell
$Version = "1.17.2"
$Tag = "v$Version"

.\package.ps1

$Package = Get-ChildItem -LiteralPath "dist\$Version" -Filter '*.pext' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
$LocalHash = (Get-FileHash -LiteralPath $Package.FullName -Algorithm SHA256).Hash

git diff --check
# Después de revisar que todos los cambios pertenecen a la release:
git add -A
git diff --cached --check
git commit -m "Release Audio Switcher $Version"
git push origin main

git tag -a $Tag -m "Audio Switcher $Tag"
git push origin $Tag

# Crear y revisar las notas en $NotesFile antes de continuar.
gh release create $Tag $Package.FullName `
    --repo Naerian/playnite-nx-audio-switcher `
    --title "Audio Switcher $Tag" `
    --notes-file $NotesFile `
    --latest `
    --verify-tag
```
