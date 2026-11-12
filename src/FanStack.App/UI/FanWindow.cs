using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FanStack.App.Interop;
using FanStack.App.Services;
using FanStack.Core.Geometry;

namespace FanStack.App.UI;

/// <summary>Voce mostrata nel ventaglio. Senza collegamento = voce speciale "Apri cartella".</summary>
internal sealed record FanEntry(string DisplayName, string? ShortcutPath)
{
    public bool IsOpenFolder => ShortcutPath is null;
}

/// <summary>Motivo della chiusura del ventaglio.</summary>
internal enum CloseReason
{
    Escape,
    Deactivated,
    OutsideClick,
    Launch,
    External,
    DisplayChanged,
}

/// <summary>
/// Finestra del ventaglio: senza bordo, trasparente, sempre in primo piano, fuori da Alt+Tab.
/// Solo presentazione: posizioni e navigazione arrivano da FanStack.Core.
/// </summary>
internal sealed class FanWindow : Window
{
    private const double HoverScale = 1.1;
    private static readonly Duration OpenDuration = TimeSpan.FromMilliseconds(320);

    private readonly IReadOnlyList<FanEntry> _entries;
    private readonly FanLayout _layout;
    private readonly ScreenContext _screen;
    private readonly Theme _theme;
    private readonly FanLayoutOptions _opt;
    private readonly List<ItemVisual> _items = [];
    private readonly Canvas _root = new();
    private readonly DispatcherTimer _outsideClickTimer;
    private readonly Rect2 _bounds;
    private Border? _popup;
    private int _selected = -1;
    private bool _closing;
    private bool _activatedOnce;
    private bool _mouseWasDown;
    private IntPtr _hwnd;

    public event Action<int>? ItemInvoked;
    public event Action<CloseReason>? CloseFinished;

    public FanWindow(IReadOnlyList<FanEntry> entries, FanLayout layout, ScreenContext screen, Theme theme, FanLayoutOptions opt)
    {
        _entries = entries;
        _layout = layout;
        _screen = screen;
        _theme = theme;
        _opt = opt;
        _bounds = layout.Bounds;

        Title = "FanStack";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        ShowActivated = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = _bounds.Left;
        Top = _bounds.Top;
        Width = _bounds.Width;
        Height = _bounds.Height;
        UseLayoutRounding = true;
        Focusable = true;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);

        _root.Width = _bounds.Width;
        _root.Height = _bounds.Height;
        Content = _root;

        if (layout.Mode == LayoutMode.Grid)
            BuildGrid();
        else
            BuildFan();

        SourceInitialized += OnSourceInitialized;
        ContentRendered += OnContentRendered;
        Deactivated += (_, _) => { if (_activatedOnce) BeginClose(CloseReason.Deactivated); };
        Activated += (_, _) => _activatedOnce = true;
        KeyDown += OnKeyDown;
        MouseLeave += (_, _) => { if (!IsKeyboardNavigating) Select(-1); };
        DpiChanged += (_, _) => PlaceWindow();

        // Rete di sicurezza: se Windows non ci concede il primo piano, "Deactivated" non arriva mai.
        // Un clic in un punto vuoto o fuori dalla finestra chiude comunque il ventaglio.
        _outsideClickTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(40), DispatcherPriority.Input, CheckOutsideClick, Dispatcher);
    }

    /// <summary>Vero dopo l'uso delle frecce, finché il mouse non si muove su un'altra voce.</summary>
    private bool IsKeyboardNavigating { get; set; }

    public void SetIcon(int index, BitmapSource image)
    {
        if (index < 0 || index >= _items.Count)
            return;
        var img = _items[index].Image;
        img.Source = image;
        img.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(120)));
    }

    // ---------- Costruzione ----------

    private void BuildFan()
    {
        foreach (var item in _layout.Items)
        {
            var entry = _entries[item.Index];
            var visual = CreateItem(item, entry, pill: true);

            var icon = item.IconRect.Offset(-_bounds.Left, -_bounds.Top);
            Canvas.SetLeft(visual.IconHost, icon.Left);
            Canvas.SetTop(visual.IconHost, icon.Top);
            _root.Children.Add(visual.IconHost);

            var label = item.LabelRect.Offset(-_bounds.Left, -_bounds.Top);
            Canvas.SetLeft(visual.LabelHost, label.Left);
            Canvas.SetTop(visual.LabelHost, label.Top);
            _root.Children.Add(visual.LabelHost);
            _items.Add(visual);
        }
    }

    private void BuildGrid()
    {
        var grid = _layout.Grid!;
        var popupRect = grid.Popup.Offset(-_bounds.Left, -_bounds.Top);
        var content = new Canvas { Width = grid.Popup.Width, Height = grid.ContentHeight + 2 * grid.Padding };
        foreach (var item in _layout.Items)
        {
            var visual = CreateItem(item, _entries[item.Index], pill: false);
            var icon = item.IconRect.Offset(-grid.Popup.Left, -grid.Popup.Top);
            Canvas.SetLeft(visual.IconHost, icon.Left);
            Canvas.SetTop(visual.IconHost, icon.Top);
            content.Children.Add(visual.IconHost);
            var label = item.LabelRect.Offset(-grid.Popup.Left, -grid.Popup.Top);
            Canvas.SetLeft(visual.LabelHost, label.Left);
            Canvas.SetTop(visual.LabelHost, label.Top);
            content.Children.Add(visual.LabelHost);
            _items.Add(visual);
        }

        var scroller = new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = grid.NeedsScroll ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false,
            CanContentScroll = false,
        };
        _popup = new Border
        {
            Width = popupRect.Width,
            Height = popupRect.Height,
            CornerRadius = new CornerRadius(18),
            Background = new SolidColorBrush(_theme.PopupBackground),
            BorderBrush = new SolidColorBrush(_theme.PopupBorder),
            BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 4, Direction = 270, Opacity = 0.35, RenderingBias = RenderingBias.Performance },
            Child = new Border { CornerRadius = new CornerRadius(17), ClipToBounds = true, Child = scroller },
            RenderTransformOrigin = OriginFraction(popupRect),
            RenderTransform = new ScaleTransform(1, 1),
            Opacity = 0,
        };
        Canvas.SetLeft(_popup, popupRect.Left);
        Canvas.SetTop(_popup, popupRect.Top);
        _root.Children.Add(_popup);
    }

    /// <summary>Punto di origine (taskbar) relativo al popup, per far "uscire" la griglia dalla taskbar.</summary>
    private Point OriginFraction(Rect2 popupLocal)
    {
        var ox = (_layout.Origin.X - _bounds.Left - popupLocal.Left) / popupLocal.Width;
        var oy = (_layout.Origin.Y - _bounds.Top - popupLocal.Top) / popupLocal.Height;
        return new Point(Math.Clamp(ox, 0, 1), Math.Clamp(oy, 0, 1));
    }

    private ItemVisual CreateItem(FanItem item, FanEntry entry, bool pill)
    {
        var size = _opt.IconSize;
        var plate = new Border
        {
            Width = size + 12,
            Height = size + 12,
            Margin = new Thickness(-6),
            CornerRadius = new CornerRadius(14),
            Background = new SolidColorBrush(_theme.SelectionPlate),
            Opacity = 0,
            IsHitTestVisible = false,
        };
        var image = new Image
        {
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            Opacity = 0,
            Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 3, Direction = 270, Opacity = 0.4, RenderingBias = RenderingBias.Performance },
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

        var scale = new ScaleTransform(1, 1);
        var rotate = new RotateTransform(item.RotationDegrees);
        var translate = new TranslateTransform();
        var iconHost = new Grid
        {
            Width = size,
            Height = size,
            Background = Brushes.Transparent, // area cliccabile anche sui pixel trasparenti dell'icona
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new TransformGroup { Children = { scale, rotate, translate } },
            Cursor = Cursors.Hand,
            ToolTip = entry.IsOpenFolder ? null : entry.DisplayName,
        };
        iconHost.Children.Add(plate);
        iconHost.Children.Add(image);

        var text = new TextBlock
        {
            Text = entry.DisplayName,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = 12.5,
            FontWeight = FontWeights.Medium,
        };
        FrameworkElement labelContent;
        if (pill)
        {
            text.Foreground = new SolidColorBrush(_theme.PillText);
            labelContent = new Border
            {
                Background = new SolidColorBrush(_theme.PillBackground),
                BorderBrush = new SolidColorBrush(_theme.PillBorder),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 3, 10, 4),
                Child = text,
                MaxWidth = item.LabelRect.Width,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = item.LabelSide == LabelSide.Left ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 1, Direction = 270, Opacity = 0.25, RenderingBias = RenderingBias.Performance },
            };
        }
        else
        {
            text.Foreground = new SolidColorBrush(_theme.GridText);
            text.TextAlignment = TextAlignment.Center;
            text.FontSize = 12;
            labelContent = new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(4, 1, 4, 2),
                Child = text,
                MaxWidth = item.LabelRect.Width,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
            };
        }

        var labelHost = new Grid
        {
            Width = item.LabelRect.Width,
            Height = item.LabelRect.Height,
            RenderTransform = translate, // l'etichetta scorre insieme all'icona durante l'animazione
            Opacity = 0,
            Cursor = Cursors.Hand,
        };
        labelHost.Children.Add(labelContent);

        var visual = new ItemVisual(item, iconHost, image, plate, labelHost, labelContent, scale, rotate, translate);
        foreach (var el in new UIElement[] { iconHost, labelContent })
        {
            el.MouseEnter += (_, _) => { IsKeyboardNavigating = false; Select(item.Index); };
            el.MouseLeftButtonUp += (_, e) => { e.Handled = true; Invoke(item.Index); };
        }
        return visual;
    }

    // ---------- Posizionamento ----------

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        // Finestra strumento: niente pulsante sulla taskbar e niente Alt+Tab.
        var ex = (long)NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE);
        ex = (ex | NativeMethods.WS_EX_TOOLWINDOW) & ~NativeMethods.WS_EX_APPWINDOW;
        NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE, (IntPtr)ex);
        PlaceWindow();
    }

    /// <summary>Posiziona la finestra in pixel fisici sul monitor giusto (WPF da solo sbaglia con DPI misti).</summary>
    private void PlaceWindow()
    {
        if (_hwnd == IntPtr.Zero)
            return;
        var px = _screen.ToPhysical(_bounds);
        NativeMethods.SetWindowPos(_hwnd, IntPtr.Zero, px.Left, px.Top, px.Right - px.Left, px.Bottom - px.Top,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    private void OnContentRendered(object? sender, EventArgs e)
    {
        Activate();
        NativeMethods.SetForegroundWindow(_hwnd);
        Focus();
        Keyboard.Focus(this);
        _outsideClickTimer.Start();
        Log.Info($"Ventaglio visibile dopo {(DateTime.Now - Program.StartTime).TotalMilliseconds:0} ms (voci={_entries.Count}, modo={_layout.Mode}).");
        PlayOpen();
    }

    // ---------- Animazioni ----------

    private void PlayOpen()
    {
        // Diagnostica: frame disegnati durante l'animazione di apertura (fluidità), scritti nel log.
        var frames = 0;
        var started = DateTime.Now;
        EventHandler counter = (_, _) => frames++;
        CompositionTarget.Rendering += counter;
        Program.After(TimeSpan.FromMilliseconds(500), () =>
        {
            CompositionTarget.Rendering -= counter;
            Log.Info($"Animazione: {frames} frame in {(DateTime.Now - started).TotalMilliseconds:0} ms.");
        });

        var ease = new BackEase { Amplitude = 0.28, EasingMode = EasingMode.EaseOut };
        var fade = new CubicEase { EasingMode = EasingMode.EaseOut };

        if (_popup is not null)
        {
            var popupScale = (ScaleTransform)_popup.RenderTransform;
            var grow = new DoubleAnimation(0.88, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            popupScale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            popupScale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            _popup.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)) { EasingFunction = fade });
        }

        foreach (var v in _items)
        {
            var begin = TimeSpan.FromMilliseconds(v.Item.DelayMs);
            if (_popup is null)
            {
                // Ventaglio: ogni icona parte dal punto della taskbar e raggiunge la sua posizione.
                var dx = _layout.Origin.X - v.Item.Center.X;
                var dy = _layout.Origin.Y - v.Item.Center.Y;
                v.Translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(dx, 0, OpenDuration) { BeginTime = begin, EasingFunction = ease, FillBehavior = FillBehavior.HoldEnd });
                v.Translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(dy, 0, OpenDuration) { BeginTime = begin, EasingFunction = ease, FillBehavior = FillBehavior.HoldEnd });
                v.Rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, v.Item.RotationDegrees, OpenDuration) { BeginTime = begin, EasingFunction = ease });
                AnimateScale(v.Scale, 0.35, 1, OpenDuration, begin, ease);
            }
            else
            {
                AnimateScale(v.Scale, 0.6, 1, TimeSpan.FromMilliseconds(240), begin + TimeSpan.FromMilliseconds(40), ease);
            }
            v.IconHost.Opacity = 0;
            v.IconHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(170)) { BeginTime = begin, EasingFunction = fade });
            v.LabelHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)) { BeginTime = begin + TimeSpan.FromMilliseconds(90), EasingFunction = fade });
        }
    }

    private static void AnimateScale(ScaleTransform t, double from, double to, Duration d, TimeSpan begin, IEasingFunction ease)
    {
        var a = new DoubleAnimation(from, to, d) { BeginTime = begin, EasingFunction = ease };
        t.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        t.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }

    /// <summary>Animazione di chiusura breve, poi notifica <see cref="CloseFinished"/>.</summary>
    public void BeginClose(CloseReason reason)
    {
        if (_closing)
            return;
        _closing = true;
        _outsideClickTimer.Stop();
        Log.Info($"Chiusura: {reason}");

        var d = TimeSpan.FromMilliseconds(reason == CloseReason.Launch ? 110 : 140);
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var fadeOut = new DoubleAnimation(0, d) { EasingFunction = ease };
        fadeOut.Completed += (_, _) =>
        {
            Hide();
            CloseFinished?.Invoke(reason);
        };
        _root.BeginAnimation(OpacityProperty, fadeOut);

        foreach (var v in _items)
        {
            var target = reason == CloseReason.Launch && v.Item.Index == _selected ? 1.25 : 0.8;
            var current = v.Scale.ScaleX;
            v.Scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(current, target, d) { EasingFunction = ease });
            v.Scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(current, target, d) { EasingFunction = ease });
        }
    }

    // ---------- Selezione e input ----------

    private void Select(int index)
    {
        if (_closing || index == _selected)
            return;
        var old = _selected;
        _selected = index;
        if (old >= 0 && old < _items.Count)
            Highlight(_items[old], false);
        if (index >= 0 && index < _items.Count)
            Highlight(_items[index], true);
    }

    private void Highlight(ItemVisual v, bool on)
    {
        var d = TimeSpan.FromMilliseconds(130);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var from = v.Scale.ScaleX;
        var to = on ? HoverScale : 1.0;
        v.Scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(from, to, d) { EasingFunction = ease });
        v.Scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(from, to, d) { EasingFunction = ease });
        v.Plate.BeginAnimation(OpacityProperty, new DoubleAnimation(on ? 1 : 0, d));
        if (v.LabelContent is Border b)
        {
            if (_popup is null)
                b.BorderBrush = new SolidColorBrush(on ? _theme.Accent : _theme.PillBorder);
            else
                b.Background = on ? new SolidColorBrush(_theme.Accent) : Brushes.Transparent;
            if (_popup is not null && b.Child is TextBlock t)
                t.Foreground = new SolidColorBrush(on ? Colors.White : _theme.GridText);
        }
        if (on)
            v.IconHost.BringIntoView();
    }

    private void Invoke(int index)
    {
        if (_closing || index < 0 || index >= _items.Count)
            return;
        Select(index);
        ItemInvoked?.Invoke(index);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                BeginClose(CloseReason.Escape);
                break;
            case Key.Enter:
            case Key.Space:
                if (_selected >= 0)
                    Invoke(_selected);
                break;
            case Key.Up:
            case Key.Down:
            case Key.Left:
            case Key.Right:
            {
                IsKeyboardNavigating = true;
                var centers = _layout.Items.Select(i => i.Center).ToList();
                var next = _selected < 0
                    ? KeyboardNavigator.Initial(centers, _layout.Origin)
                    : KeyboardNavigator.Next(centers, _selected, e.Key switch
                    {
                        Key.Up => NavDirection.Up,
                        Key.Down => NavDirection.Down,
                        Key.Left => NavDirection.Left,
                        _ => NavDirection.Right,
                    });
                Select(next);
                break;
            }
            default:
                return;
        }
        e.Handled = true;
    }

    /// <summary>Chiude se viene premuto un pulsante del mouse fuori dalle voci del ventaglio.</summary>
    private void CheckOutsideClick(object? sender, EventArgs e)
    {
        if (_closing || _hwnd == IntPtr.Zero)
            return;
        var down = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LBUTTON) & 0x8000) != 0
                   || (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RBUTTON) & 0x8000) != 0
                   || (NativeMethods.GetAsyncKeyState(NativeMethods.VK_MBUTTON) & 0x8000) != 0;
        var pressed = down && !_mouseWasDown;
        _mouseWasDown = down;
        if (!pressed || !NativeMethods.GetCursorPos(out var p))
            return;

        var local = PointFromScreen(new Point(p.X, p.Y));
        var inside = local.X >= 0 && local.Y >= 0 && local.X <= ActualWidth && local.Y <= ActualHeight;
        if (inside && HitsContent(local))
            return;
        BeginClose(CloseReason.OutsideClick);
    }

    private bool HitsContent(Point local)
    {
        var hit = false;
        VisualTreeHelper.HitTest(_root, null, r =>
        {
            hit = true;
            return HitTestResultBehavior.Stop;
        }, new PointHitTestParameters(local));
        return hit;
    }

    protected override void OnClosed(EventArgs e)
    {
        _outsideClickTimer.Stop();
        base.OnClosed(e);
    }

    private sealed record ItemVisual(
        FanItem Item,
        Grid IconHost,
        Image Image,
        Border Plate,
        Grid LabelHost,
        FrameworkElement LabelContent,
        ScaleTransform Scale,
        RotateTransform Rotate,
        TranslateTransform Translate);
}
