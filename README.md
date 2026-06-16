# QuickPreview

**Previsualiza cualquier archivo al instante desde el Explorador de Windows.**

Selecciona un archivo en el Explorador y presiona `Espacio` — aparece una ventana de preview sin necesidad de abrir ninguna aplicación. Igual que Quick Look en macOS, pero nativo en Windows.

> Disponible gratis en la [Microsoft Store](#) *(próximamente)*

---

## Formatos soportados

| Categoría | Extensiones |
|-----------|-------------|
| **Imágenes** | JPG, PNG, GIF, BMP, WEBP, TIFF, ICO, SVG |
| **RAW** | ARW, NEF, CR2, CR3, RAF, ORF, RW2, DNG, HEIC/HEIF y más |
| **Vídeo** | MP4, MKV, MOV, AVI, WMV, WEBM, FLV y más |
| **Audio** | MP3, FLAC, WAV, AAC, OGG, OPUS, WMA, AIFF |
| **PDF** | Visor nativo con zoom y desplazamiento |
| **Office** | DOCX, DOC, XLSX, XLS, PPTX, PPT |
| **Código** | 50+ lenguajes: C#, Python, JS/TS, Rust, Go, SQL… |
| **Fuentes** | TTF, OTF — espécimen tipográfico completo |
| **Archivos** | ZIP — explorador de contenido |

## Atajos de teclado

| Tecla | Acción |
|-------|--------|
| `Espacio` | Abrir · cerrar preview |
| `Esc` | Cerrar preview |
| `← →` | Navegar entre archivos de la carpeta |
| `F` | Pantalla completa |
| Rueda del ratón | Zoom en imágenes |
| Doble clic | Restablecer zoom |
| `↗` (botón) | Abrir con app predeterminada |

## Seguridad

QuickPreview **nunca ejecuta** el contenido de los archivos que previsualiza:

- JavaScript deshabilitado en WebView2
- Content Security Policy estricta en todo HTML generado
- SVG sanitizado (elimina `<script>`, `on*`, `javascript:`)
- Fórmulas de Excel no evaluadas (solo se muestra el texto formateado)
- Sin acceso a red — todo el preview es local

## Instalación

### Desde la Microsoft Store *(próximamente)*

### Desde el código fuente

**Requisitos:**
- Windows 10 1809 (build 17763) o superior
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Microsoft Edge / WebView2 Runtime (incluido en Windows 10/11)

```powershell
git clone https://github.com/israyosoy/QuickPreview.git
cd QuickPreview

# Generar íconos y assets
.\GenerateAssets.ps1

# Compilar y ejecutar
dotnet run --project QuickPreview\QuickPreview.csproj -c Release
```

### Empaquetar como MSIX

```powershell
# 1. Rellena Package\AppxManifest.xml con tu Publisher info
# 2. Ejecuta:
.\Pack-MSIX.ps1
```

## Stack tecnológico

- **C# 13 / .NET 10 / WPF** — nativo Windows, sin Electron
- **WebView2** — renderizado de PDF y documentos Office
- **ClosedXML** — lectura de Excel sin Office instalado
- **Mammoth** — conversión DOCX → HTML
- **DocumentFormat.OpenXml** — lectura de PowerPoint

## Contribuir

Los pull requests son bienvenidos. Para cambios importantes, abre un issue primero.

## Licencia

[MIT](LICENSE) — Uso libre, incluyendo uso comercial.
