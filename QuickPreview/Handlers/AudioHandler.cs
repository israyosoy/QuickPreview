using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using WpfButton = System.Windows.Controls.Button;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfVerticalAlignment = System.Windows.VerticalAlignment;

namespace QuickPreview.Handlers;

public class AudioHandler : IPreviewHandler
{
    public Task<UIElement> CreatePreviewAsync(string filePath)
        => Task.FromResult(CreateAudioUI(filePath));

    private static UIElement CreateAudioUI(string filePath)
    {
        // Hidden MediaElement for audio-only playback
        var media = new MediaElement
        {
            Source = new Uri(filePath),
            LoadedBehavior = MediaState.Manual,
            UnloadedBehavior = MediaState.Manual,
            Volume = 1.0,
            Width = 0,
            Height = 0
        };

        // ── Controls ─────────────────────────────────────────────────────────
        var playBtn = BuildButton("▶");
        var muteBtn = BuildButton("🔊");

        var slider = new Slider
        {
            Minimum = 0, Maximum = 100, Value = 0,
            IsMoveToPointEnabled = true,
            Margin = new Thickness(6, 0, 6, 0),
            VerticalAlignment = WpfVerticalAlignment.Center
        };

        var timeLabel = new TextBlock
        {
            Text = "0:00 / 0:00",
            Foreground = new SolidColorBrush(WpfColor.FromRgb(140, 140, 140)),
            FontSize = 11,
            FontFamily = new WpfFontFamily("Consolas"),
            VerticalAlignment = WpfVerticalAlignment.Center,
            MinWidth = 96
        };

        // ── Artwork / info area ───────────────────────────────────────────────
        string name = Path.GetFileNameWithoutExtension(filePath);
        string ext  = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant();

        var artPanel = new Border
        {
            Width = 440,
            Height = 200,
            Background = new SolidColorBrush(WpfColor.FromRgb(20, 20, 20)),
            Child = new StackPanel
            {
                VerticalAlignment = WpfVerticalAlignment.Center,
                HorizontalAlignment = WpfHorizontalAlignment.Center,
                Children =
                {
                    new TextBlock
                    {
                        Text = "♪",
                        FontSize = 64,
                        Foreground = new SolidColorBrush(WpfColor.FromRgb(60, 130, 220)),
                        HorizontalAlignment = WpfHorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 16)
                    },
                    new TextBlock
                    {
                        Text = name,
                        FontSize = 14,
                        Foreground = new SolidColorBrush(WpfColor.FromRgb(210, 210, 210)),
                        FontFamily = new WpfFontFamily("Segoe UI"),
                        HorizontalAlignment = WpfHorizontalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 360,
                        TextAlignment = TextAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = ext,
                        FontSize = 11,
                        Foreground = new SolidColorBrush(WpfColor.FromRgb(100, 100, 100)),
                        FontFamily = new WpfFontFamily("Consolas"),
                        HorizontalAlignment = WpfHorizontalAlignment.Center,
                        Margin = new Thickness(0, 6, 0, 0)
                    }
                }
            }
        };

        // ── Control row ───────────────────────────────────────────────────────
        var ctrlGrid = new Grid();
        ctrlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ctrlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ctrlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ctrlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(playBtn, 0); Grid.SetColumn(slider, 1);
        Grid.SetColumn(timeLabel, 2); Grid.SetColumn(muteBtn, 3);
        ctrlGrid.Children.Add(playBtn); ctrlGrid.Children.Add(slider);
        ctrlGrid.Children.Add(timeLabel); ctrlGrid.Children.Add(muteBtn);

        var controlPanel = new Border
        {
            Background = new SolidColorBrush(WpfColor.FromRgb(28, 28, 28)),
            Padding = new Thickness(8, 5, 8, 5),
            Child = ctrlGrid
        };

        // ── Layout ────────────────────────────────────────────────────────────
        var outer = new Grid();
        outer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(artPanel, 0); Grid.SetRow(controlPanel, 1);
        outer.Children.Add(media);   // Hidden MediaElement must be in visual tree
        outer.Children.Add(artPanel);
        outer.Children.Add(controlPanel);

        // ── State / events ────────────────────────────────────────────────────
        bool isPlaying = false, isMuted = false, isSeeking = false;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };

        media.MediaOpened += (_, _) =>
        {
            if (media.NaturalDuration.HasTimeSpan)
                slider.Maximum = media.NaturalDuration.TimeSpan.TotalSeconds;
            media.Play(); isPlaying = true; playBtn.Content = "⏸"; timer.Start();
        };
        media.MediaEnded += (_, _) =>
        {
            isPlaying = false; playBtn.Content = "▶";
            media.Position = TimeSpan.Zero; slider.Value = 0;
        };
        media.MediaFailed += (_, _) => timer.Stop();

        timer.Tick += (_, _) =>
        {
            if (isSeeking || !media.NaturalDuration.HasTimeSpan) return;
            slider.Value = media.Position.TotalSeconds;
            timeLabel.Text = $"{Fmt(media.Position)} / {Fmt(media.NaturalDuration.TimeSpan)}";
        };

        playBtn.Click += (_, _) =>
        {
            if (isPlaying) { media.Pause(); playBtn.Content = "▶"; }
            else { media.Play(); playBtn.Content = "⏸"; }
            isPlaying = !isPlaying;
        };
        muteBtn.Click += (_, _) =>
        {
            isMuted = !isMuted; media.IsMuted = isMuted;
            muteBtn.Content = isMuted ? "🔇" : "🔊";
        };
        slider.PreviewMouseLeftButtonDown += (_, _) => isSeeking = true;
        slider.PreviewMouseLeftButtonUp += (_, _) =>
        {
            isSeeking = false;
            if (media.NaturalDuration.HasTimeSpan) media.Position = TimeSpan.FromSeconds(slider.Value);
        };
        slider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((_, _) =>
        {
            isSeeking = false;
            if (media.NaturalDuration.HasTimeSpan) media.Position = TimeSpan.FromSeconds(slider.Value);
        }));

        media.Loaded += (_, _) =>
        {
            var win = Window.GetWindow(media);
            if (win != null) win.Closed += (_, _) => { timer.Stop(); media.Stop(); media.Close(); };
        };

        return outer;
    }

    private static WpfButton BuildButton(string content)
    {
        var btn = new WpfButton
        {
            Content = content, FontSize = 14, Width = 32, Height = 28,
            Cursor = System.Windows.Input.Cursors.Hand,
            VerticalAlignment = WpfVerticalAlignment.Center,
            Padding = new Thickness(0)
        };
        var tmpl = new ControlTemplate(typeof(WpfButton));
        var bd = new FrameworkElementFactory(typeof(Border));
        bd.Name = "bd";
        bd.SetValue(Border.BackgroundProperty, WpfBrushes.Transparent);
        bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, WpfHorizontalAlignment.Center);
        cp.SetValue(ContentPresenter.VerticalAlignmentProperty, WpfVerticalAlignment.Center);
        bd.AppendChild(cp);
        tmpl.VisualTree = bd;
        var hover = new Trigger { Property = WpfButton.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty,
            new SolidColorBrush(WpfColor.FromArgb(60, 255, 255, 255)), "bd"));
        tmpl.Triggers.Add(hover);
        btn.Template = tmpl;
        btn.Foreground = new SolidColorBrush(WpfColor.FromRgb(200, 200, 200));
        return btn;
    }

    private static string Fmt(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}" : $"{t.Minutes}:{t.Seconds:D2}";
}
