#Requires -Version 5.1
<#
.SYNOPSIS
    Genera las imagenes de marketing para el listing de la Microsoft Store.
.OUTPUT
    Store\StoreLogo_300x300.png
    Store\AppTileIcon_358x358.png
    Store\BoxArt_1080x1080.png
    Store\PosterArt_792x1080.png
#>

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$OutDir      = Join-Path $ProjectRoot "Store"
New-Item -ItemType Directory -Force $OutDir | Out-Null

Add-Type -AssemblyName System.Drawing

$csCode = @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

public static class StoreArt {

    static Color BG      = Color.FromArgb(255,  13,  17,  23);
    static Color BG2     = Color.FromArgb(255,  22,  27,  34);
    static Color ACCENT  = Color.FromArgb(255,  31, 111, 235);
    static Color ACCENT2 = Color.FromArgb(255,  88, 166, 255);
    static Color WHITE   = Color.FromArgb(255, 240, 246, 252);
    static Color GRAY    = Color.FromArgb(255, 139, 148, 158);

    static void FillBg(Graphics g, int w, int h) {
        g.Clear(BG);
        LinearGradientBrush gb = new LinearGradientBrush(
            new PointF(0, 0), new PointF(w * 0.6f, h * 0.6f),
            Color.FromArgb(40, 31, 111, 235), Color.Transparent);
        g.FillRectangle(gb, 0, 0, w, h);
        gb.Dispose();
    }

    static void DrawIcon(Graphics g, int cx, int cy, int r, float sw) {
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Pen p; SolidBrush b;

        p = new Pen(Color.FromArgb(35, 88, 166, 255), sw * 3);
        g.DrawEllipse(p, cx-r-sw, cy-r-sw, (r+sw)*2, (r+sw)*2);
        p.Dispose();

        b = new SolidBrush(BG2);
        g.FillEllipse(b, cx-r, cy-r, r*2, r*2);
        b.Dispose();

        p = new Pen(ACCENT, sw);
        g.DrawEllipse(p, cx-r, cy-r, r*2, r*2);
        p.Dispose();

        int ea = (int)(r * 0.65f), eb = (int)(r * 0.42f);
        p = new Pen(ACCENT2, sw * 0.7f);
        g.DrawEllipse(p, cx-ea, cy-eb, ea*2, eb*2);
        p.Dispose();

        int ir = (int)(r * 0.28f);
        b = new SolidBrush(ACCENT);
        g.FillEllipse(b, cx-ir, cy-ir, ir*2, ir*2);
        b.Dispose();

        int pr = (int)(r * 0.12f);
        b = new SolidBrush(BG);
        g.FillEllipse(b, cx-pr, cy-pr, pr*2, pr*2);
        b.Dispose();

        int glr = (int)(r * 0.06f);
        int gx = cx + (int)(ir * 0.5f), gy = cy - (int)(ir * 0.5f);
        b = new SolidBrush(Color.FromArgb(200, 255, 255, 255));
        g.FillEllipse(b, gx-glr, gy-glr, glr*2, glr*2);
        b.Dispose();

        int hx1 = cx + (int)(r*0.65f), hy1 = cy + (int)(r*0.65f);
        int hx2 = cx + (int)(r*1.08f), hy2 = cy + (int)(r*1.08f);
        p = new Pen(ACCENT, sw);
        p.StartCap = LineCap.Round;
        p.EndCap   = LineCap.Round;
        g.DrawLine(p, hx1, hy1, hx2, hy2);
        p.Dispose();
    }

    static StringFormat CenterFmt() {
        StringFormat sf = new StringFormat();
        sf.Alignment = StringAlignment.Center;
        sf.LineAlignment = StringAlignment.Center;
        return sf;
    }

    public static void MakeStoreLogo(string path) {
        int w = 300, h = 300;
        Bitmap bmp = new Bitmap(w, h);
        Graphics g = Graphics.FromImage(bmp);
        FillBg(g, w, h);
        DrawIcon(g, 140, 145, 88, 5.5f);
        g.Dispose();
        bmp.Save(path, ImageFormat.Png);
        bmp.Dispose();
    }

    public static void MakeAppTile(string path) {
        int w = 358, h = 358;
        Bitmap bmp = new Bitmap(w, h);
        Graphics g = Graphics.FromImage(bmp);
        FillBg(g, w, h);
        DrawIcon(g, 168, 173, 105, 6.5f);
        g.Dispose();
        bmp.Save(path, ImageFormat.Png);
        bmp.Dispose();
    }

    public static void MakeBoxArt(string path) {
        int w = 1080, h = 1080;
        Bitmap bmp = new Bitmap(w, h);
        Graphics g = Graphics.FromImage(bmp);
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        FillBg(g, w, h);
        DrawIcon(g, 540, 390, 200, 13f);

        StringFormat fmt = CenterFmt();

        Font f1 = new Font("Segoe UI", 78, FontStyle.Bold);
        SolidBrush bw = new SolidBrush(WHITE);
        g.DrawString("QuickPreview", f1, bw, new RectangleF(0, 635, w, 110), fmt);
        f1.Dispose();

        Font f2 = new Font("Segoe UI", 30, FontStyle.Regular);
        SolidBrush bgr = new SolidBrush(GRAY);
        g.DrawString("Instant File Preview for Windows", f2, bgr, new RectangleF(0, 755, w, 55), fmt);
        f2.Dispose();

        string[] tags = { "Images + RAW", "PDF + Office", "Video + Audio", "Code + Fonts" };
        int tw = 220, th = 42, gap = 14;
        int startX = (w - (tw * tags.Length + gap * (tags.Length - 1))) / 2;
        Font tf = new Font("Segoe UI", 20, FontStyle.Regular);
        SolidBrush tAccent = new SolidBrush(ACCENT);
        for (int i = 0; i < tags.Length; i++) {
            int tx = startX + i * (tw + gap);
            g.FillRectangle(tAccent, tx, 845, tw, th);
            g.DrawString(tags[i], tf, bw, new RectangleF(tx, 845, tw, th), fmt);
        }
        tf.Dispose(); tAccent.Dispose(); bw.Dispose(); bgr.Dispose(); fmt.Dispose();

        g.Dispose();
        bmp.Save(path, ImageFormat.Png);
        bmp.Dispose();
    }

    public static void MakePosterArt(string path) {
        int w = 792, h = 1080;
        Bitmap bmp = new Bitmap(w, h);
        Graphics g = Graphics.FromImage(bmp);
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        FillBg(g, w, h);
        DrawIcon(g, 396, 360, 200, 13f);

        StringFormat fmt = CenterFmt();
        SolidBrush bw  = new SolidBrush(WHITE);
        SolidBrush bgr = new SolidBrush(GRAY);

        Font f1 = new Font("Segoe UI", 68, FontStyle.Bold);
        g.DrawString("QuickPreview", f1, bw, new RectangleF(0, 610, w, 105), fmt);
        f1.Dispose();

        Font f2 = new Font("Segoe UI", 27, FontStyle.Regular);
        g.DrawString("Instant File Preview for Windows", f2, bgr, new RectangleF(0, 720, w, 52), fmt);
        f2.Dispose();

        Pen divPen = new Pen(Color.FromArgb(60, 88, 166, 255), 1.5f);
        g.DrawLine(divPen, 100, 796, 692, 796);
        divPen.Dispose();

        string[] lines = {
            "Press Space in File Explorer to preview any file",
            "Images, RAW, Video, Audio, PDF, Office, Code, Fonts",
            "No internet  -  No telemetry  -  Free"
        };
        Font f3 = new Font("Segoe UI", 21, FontStyle.Regular);
        int fy = 820;
        foreach (string line in lines) {
            g.DrawString(line, f3, bgr, new RectangleF(0, fy, w, 40), fmt);
            fy += 44;
        }
        f3.Dispose(); bw.Dispose(); bgr.Dispose(); fmt.Dispose();

        g.Dispose();
        bmp.Save(path, ImageFormat.Png);
        bmp.Dispose();
    }
}
'@

Add-Type -TypeDefinition $csCode -ReferencedAssemblies "System.Drawing"

Write-Host "`nGenerando Store assets en $OutDir..."

[StoreArt]::MakeStoreLogo((Join-Path $OutDir "StoreLogo_300x300.png"))
Write-Host "  OK  StoreLogo_300x300.png"

[StoreArt]::MakeAppTile((Join-Path $OutDir "AppTileIcon_358x358.png"))
Write-Host "  OK  AppTileIcon_358x358.png"

[StoreArt]::MakeBoxArt((Join-Path $OutDir "BoxArt_1080x1080.png"))
Write-Host "  OK  BoxArt_1080x1080.png"

[StoreArt]::MakePosterArt((Join-Path $OutDir "PosterArt_792x1080.png"))
Write-Host "  OK  PosterArt_792x1080.png"

Write-Host "`nListo. Archivos en: $OutDir"
Write-Host "Sube estos 4 en Partner Center > Listing > Store logos / Promotional images"
Write-Host "Screenshots (min 1366x768): tomalos con la app abierta y anyadelos tambien`n"
