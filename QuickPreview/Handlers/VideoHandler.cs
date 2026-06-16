using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
// Aliases to resolve System.Drawing / System.Windows.Forms conflicts
using WpfButton = System.Windows.Controls.Button;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfCursors = System.Windows.Input.Cursors;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfVerticalAlignment = System.Windows.VerticalAlignment;

namespace QuickPreview.Handlers;

public class VideoHandler : IPreviewHandler
{
    public Task<UIElement> CreatePreviewAsync(string filePath)
        => Task.FromResult(CreateVideoUI(filePath));

    private UIElement CreateVideoUI(string filePath)
    {
        var container = new Grid();
        container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Black background behind the video
        var videoBg = new Border { Background = new SolidColorBrush(Colors.Black) };

        var media = new MediaElement
        {
            Source = new Uri(filePath),
            LoadedBehavior = MediaState.Manual,
            UnloadedBehavior = MediaState.Manual,
            Stretch = Stretch.Uniform,
            Width = 854,
            Height = 480,
            SnapsToDevicePixels = true
        };

        videoBg.Child = media;

        // ── Controls ────────────────────────────────────────────────────────
        var playBtn = BuildButton("⏸");
        var muteBtn = BuildButton("🔊");

        var slider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            IsMoveToPointEnabled = true,
            Margin = new Thickness(6, 0, 6, 0),
            VerticalAlignment = WpfVerticalAlignment.Center
        };

        var timeLabel = new TextBlock
        {
            Text = "0:00 / 0:00",
            Foreground = new SolidColorBrush(WpfColor.FromRgb(160, 160, 160)),
            FontSize = 11,
            FontFamily = new WpfFontFamily("Consolas"),
            VerticalAlignment = WpfVerticalAlignment.Center,
            MinWidth = 96
        };

        var controlPanel = new Border
        {
            Background = new SolidColorBrush(WpfColor.FromRgb(28, 28, 28)),
            Padding = new Thickness(8, 5, 8, 5),
            Child = BuildControlRow(playBtn, slider, timeLabel, muteBtn)
        };

        Grid.SetRow(videoBg, 0);
        Grid.SetRow(controlPanel, 1);
        container.Children.Add(videoBg);
        container.Children.Add(controlPanel);

        // ── State ────────────────────────────────────────────────────────────
        bool isPlaying = false;
        bool isMuted = false;
        bool isSeeking = false;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };

        // ── MediaElement events ──────────────────────────────────────────────
        media.MediaOpened += (_, _) =>
        {
            // Resize to actual video dimensions (capped at max)
            if (media.NaturalVideoWidth > 0)
            {
                const double MaxW = 960, MaxH = 640;
                double scale = Math.Min(MaxW / media.NaturalVideoWidth, MaxH / media.NaturalVideoHeight);
                scale = Math.Min(scale, 1.0);
                media.Width = media.NaturalVideoWidth * scale;
                media.Height = media.NaturalVideoHeight * scale;
            }

            if (media.NaturalDuration.HasTimeSpan)
                slider.Maximum = media.NaturalDuration.TimeSpan.TotalSeconds;

            media.Play();
            isPlaying = true;
            playBtn.Content = "⏸";
            timer.Start();
        };

        media.MediaEnded += (_, _) =>
        {
            isPlaying = false;
            playBtn.Content = "▶";
            media.Position = TimeSpan.Zero;
            slider.Value = 0;
        };

        media.MediaFailed += (_, e) =>
        {
            timer.Stop();
            var msg = new TextBlock
            {
                Text = $"No se pudo reproducir el video.\n{e.ErrorException?.Message}\n\n" +
                       "Puede faltar un codec. Instala K-Lite Codec Pack para soporte extendido.",
                Foreground = new SolidColorBrush(WpfColor.FromRgb(180, 80, 80)),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(24),
                MaxWidth = 480
            };
            container.Children.Remove(videoBg);
            Grid.SetRow(msg, 0);
            container.Children.Insert(0, msg);
        };

        // ── Timer ────────────────────────────────────────────────────────────
        timer.Tick += (_, _) =>
        {
            if (isSeeking || !media.NaturalDuration.HasTimeSpan) return;
            slider.Value = media.Position.TotalSeconds;
            timeLabel.Text = $"{FormatTime(media.Position)} / {FormatTime(media.NaturalDuration.TimeSpan)}";
        };

        // ── Play / Pause ─────────────────────────────────────────────────────
        playBtn.Click += (_, _) =>
        {
            if (isPlaying) { media.Pause(); playBtn.Content = "▶"; }
            else { media.Play(); playBtn.Content = "⏸"; }
            isPlaying = !isPlaying;
        };

        // ── Mute ─────────────────────────────────────────────────────────────
        muteBtn.Click += (_, _) =>
        {
            isMuted = !isMuted;
            media.IsMuted = isMuted;
            muteBtn.Content = isMuted ? "🔇" : "🔊";
        };

        // ── Seek ─────────────────────────────────────────────────────────────
        slider.PreviewMouseLeftButtonDown += (_, _) => isSeeking = true;
        slider.PreviewMouseLeftButtonUp += (_, _) =>
        {
            isSeeking = false;
            if (media.NaturalDuration.HasTimeSpan)
                media.Position = TimeSpan.FromSeconds(slider.Value);
        };
        slider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((_, _) =>
        {
            isSeeking = false;
            if (media.NaturalDuration.HasTimeSpan)
                media.Position = TimeSpan.FromSeconds(slider.Value);
        }));

        // ── Cleanup ──────────────────────────────────────────────────────────
        // Unloaded fires too late / unreliably on window close; hook Window.Closed instead.
        media.Loaded += (_, _) =>
        {
            var win = Window.GetWindow(media);
            if (win != null)
                win.Closed += (_, _) => { timer.Stop(); media.Stop(); media.Close(); };
        };

        return container;
    }

    private static Grid BuildControlRow(WpfButton playBtn, Slider slider, TextBlock timeLabel, WpfButton muteBtn)
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(playBtn, 0);
        Grid.SetColumn(slider, 1);
        Grid.SetColumn(timeLabel, 2);
        Grid.SetColumn(muteBtn, 3);

        g.Children.Add(playBtn);
        g.Children.Add(slider);
        g.Children.Add(timeLabel);
        g.Children.Add(muteBtn);
        return g;
    }

    private static WpfButton BuildButton(string content)
    {
        var btn = new WpfButton
        {
            Content = content,
            FontSize = 14,
            Width = 32,
            Height = 28,
            Cursor = WpfCursors.Hand,
            VerticalAlignment = WpfVerticalAlignment.Center,
            Padding = new Thickness(0)
        };

        var template = new ControlTemplate(typeof(WpfButton));
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "bd";
        border.SetValue(Border.BackgroundProperty, WpfBrushes.Transparent);
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));

        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, WpfHorizontalAlignment.Center);
        cp.SetValue(ContentPresenter.VerticalAlignmentProperty, WpfVerticalAlignment.Center);
        border.AppendChild(cp);
        template.VisualTree = border;

        var hover = new Trigger { Property = WpfButton.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty,
            new SolidColorBrush(WpfColor.FromArgb(60, 255, 255, 255)), "bd"));
        template.Triggers.Add(hover);

        btn.Template = template;
        btn.Foreground = new SolidColorBrush(WpfColor.FromRgb(200, 200, 200));
        return btn;
    }

    private static string FormatTime(TimeSpan t) =>
        t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
            : $"{t.Minutes}:{t.Seconds:D2}";
}
