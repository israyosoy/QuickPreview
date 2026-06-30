using QuickPreview.Handlers;
using QuickPreview.Handlers.Documents;
using System.IO;

namespace QuickPreview.Services;

public static class FileTypeRouter
{
    // ── Image / Video / Audio / Font handlers ─────────────────────────────────
    private static readonly ImageHandler _imageHandler = new();
    private static readonly VideoHandler _videoHandler = new();
    private static readonly AudioHandler _audioHandler = new();
    private static readonly FontHandler  _fontHandler  = new();

    private static readonly HashSet<string> _imageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Raster (WPF native + WIC)
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif", ".ico", ".webp",
        // HEIC/HEIF — requiere "HEIF Image Extensions" (Microsoft Store)
        ".heic", ".heif",
        // RAW genérico / DNG
        ".raw", ".dng",
        // Sony
        ".arw", ".srf", ".sr2",
        // Canon
        ".cr2", ".cr3", ".crw",
        // Nikon
        ".nef", ".nrw",
        // Olympus / OM System
        ".orf",
        // Panasonic / Leica
        ".rw2", ".rwl",
        // Pentax / Ricoh
        ".pef",
        // Samsung
        ".srw",
        // Fujifilm
        ".raf",
        // Hasselblad
        ".3fr",
        // Mamiya
        ".mef",
        // Sigma
        ".x3f",
        // Minolta / Konica-Minolta
        ".mrw",
    };

    private static readonly HashSet<string> _videoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4v",
        ".mov",
        ".avi",
        ".mkv",
        ".wmv", ".asf",
        ".webm",
        ".flv",
        ".ts", ".m2ts", ".mts",
        ".mpg", ".mpeg", ".m2v",
        ".3gp", ".3g2",
        ".vob",
        ".ogv",
    };

    private static readonly HashSet<string> _audioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".m4a", ".aac",
        ".wav", ".wave",
        ".flac",
        ".ogg", ".opus",
        ".wma",
        ".aiff", ".aif",
    };

    private static readonly HashSet<string> _fontExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ttf", ".otf", ".woff",
    };

    // ── Document handlers ─────────────────────────────────────────────────────
    private static readonly PdfDocHandler       _pdfHandler  = new();
    private static readonly WordDocHandler      _wordHandler = new();
    private static readonly ExcelDocHandler     _excelHandler = new();
    private static readonly PowerPointDocHandler _pptHandler  = new();
    private static readonly TextDocHandler      _textHandler = new();
    private static readonly SvgDocHandler       _svgHandler  = new();
    private static readonly ZipDocHandler       _zipHandler  = new();
    private static readonly UnsupportedFormatDocHandler _unsupportedHandler = new();

    private static readonly Dictionary<string, IDocumentHandler> _documentHandlers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // PDF
            [".pdf"] = _pdfHandler,
            // Adobe Illustrator — modern .ai files are PDF-compatible by default,
            // so the PDF viewer renders them directly in most cases.
            [".ai"]  = _pdfHandler,
            // Adobe formats with no lightweight preview path (proprietary binary layouts)
            [".psd"]  = _unsupportedHandler,
            [".psb"]  = _unsupportedHandler,
            [".eps"]  = _unsupportedHandler,
            [".indd"] = _unsupportedHandler,
            [".aep"]  = _unsupportedHandler,
            [".prproj"] = _unsupportedHandler,
            // Word
            [".docx"] = _wordHandler,
            [".doc"]  = _wordHandler,
            // Excel
            [".xlsx"] = _excelHandler,
            [".xls"]  = _excelHandler,
            [".xlsm"] = _excelHandler,
            // PowerPoint
            [".pptx"] = _pptHandler,
            [".ppt"]  = _pptHandler,
            [".ppsx"] = _pptHandler,
            // SVG
            [".svg"]  = _svgHandler,
            [".svgz"] = _svgHandler,
            // Archivos comprimidos
            [".zip"] = _zipHandler,
            // Texto plano
            [".txt"] = _textHandler,
            [".log"] = _textHandler,
            [".csv"] = _textHandler,
            // Markdown
            [".md"]       = _textHandler,
            [".markdown"] = _textHandler,
            // Web
            [".html"] = _textHandler,
            [".htm"]  = _textHandler,
            [".css"]  = _textHandler,
            [".scss"] = _textHandler,
            [".sass"] = _textHandler,
            // Datos / Config
            [".json"] = _textHandler,
            [".xml"]  = _textHandler,
            [".yaml"] = _textHandler,
            [".yml"]  = _textHandler,
            [".toml"] = _textHandler,
            [".ini"]  = _textHandler,
            [".cfg"]  = _textHandler,
            [".conf"] = _textHandler,
            // C / C++
            [".c"]   = _textHandler,
            [".h"]   = _textHandler,
            [".cpp"] = _textHandler,
            [".cc"]  = _textHandler,
            [".cxx"] = _textHandler,
            [".hpp"] = _textHandler,
            // C#
            [".cs"]   = _textHandler,
            [".csx"]  = _textHandler,
            [".csproj"] = _textHandler,
            [".sln"]  = _textHandler,
            // JavaScript / TypeScript
            [".js"]  = _textHandler,
            [".mjs"] = _textHandler,
            [".cjs"] = _textHandler,
            [".ts"]  = _textHandler,
            [".tsx"] = _textHandler,
            [".jsx"] = _textHandler,
            // Python
            [".py"]  = _textHandler,
            [".pyw"] = _textHandler,
            // Java / Kotlin
            [".java"] = _textHandler,
            [".kt"]   = _textHandler,
            [".kts"]  = _textHandler,
            // Go
            [".go"] = _textHandler,
            // Rust
            [".rs"] = _textHandler,
            // Ruby
            [".rb"] = _textHandler,
            // PHP
            [".php"] = _textHandler,
            // Swift
            [".swift"] = _textHandler,
            // Shell
            [".sh"]  = _textHandler,
            [".zsh"] = _textHandler,
            [".bat"] = _textHandler,
            [".cmd"] = _textHandler,
            [".ps1"] = _textHandler,
            // SQL
            [".sql"] = _textHandler,
            // Vue / Svelte
            [".vue"]    = _textHandler,
            [".svelte"] = _textHandler,
            // Otros
            [".dockerfile"] = _textHandler,
            [".gitignore"]  = _textHandler,
            [".editorconfig"] = _textHandler,
        };

    // ── Public API ────────────────────────────────────────────────────────────

    public static IPreviewHandler? GetImageVideoHandler(string filePath)
    {
        string ext = Path.GetExtension(filePath);
        if (_imageExtensions.Contains(ext)) return _imageHandler;
        if (_videoExtensions.Contains(ext)) return _videoHandler;
        if (_audioExtensions.Contains(ext)) return _audioHandler;
        if (_fontExtensions.Contains(ext))  return _fontHandler;
        return null;
    }

    public static IDocumentHandler? GetDocumentHandler(string filePath)
    {
        string ext = Path.GetExtension(filePath);
        return _documentHandlers.GetValueOrDefault(ext);
    }
}
