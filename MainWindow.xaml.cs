using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace Glassboard;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private const int WmClipboardUpdate = 0x031D;
    private const int WmNcHitTest = 0x0084;
    private const int VkControl = 0x11;
    private const int HtClient = 1;
    private const int HtCaption = 2;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;
    private const double CtrlOpacity = 0.98;
    private const double DockGap = 16;
    private const double ResizeEdgeSize = 16;
    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x00000020L;
    private const int SwpNoMove = 0x0002;
    private const int SwpNoSize = 0x0001;
    private const int SwpNoZOrder = 0x0004;
    private const int SwpNoActivate = 0x0010;
    private const int SwpFrameChanged = 0x0020;
    private static readonly IntPtr HwndTopmost = new(-1);
    private static readonly IntPtr HwndNotopmost = new(-2);
    private const int WdaNone = 0;
    private const int WdaMonitor = 1;
    private const int WdaExcludeFromCapture = 0x11;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, int dwAffinity);

    private readonly DispatcherTimer _inputTimer;
    private HwndSource? _source;
    private double _overlayOpacity = 0.72;
    private bool _isOpacityDragging;
    private string? _lastClipboardSignature;
    private string? _ignoreNextClipboardSignature;
    private string? _selectedClipboardSignature;
    private ClipboardEntry? _selectedEntry;
    private bool _isSelectionExpanded;

    public ObservableCollection<ClipboardEntry> LatestImages { get; } = new();
    public ObservableCollection<ClipboardEntry> LatestTexts { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public ClipboardEntry? SelectedEntry
    {
        get => _selectedEntry;
        private set
        {
            if (ReferenceEquals(_selectedEntry, value))
                return;

            _selectedEntry = value;
            OnPropertyChanged();
        }
    }

    public bool IsSelectionExpanded
    {
        get => _isSelectionExpanded;
        private set
        {
            if (_isSelectionExpanded == value)
                return;

            _isSelectionExpanded = value;
            OnPropertyChanged();
        }
    }

    public double OverlayOpacity
    {
        get => _overlayOpacity;
        set
        {
            var clamped = Math.Clamp(value, 0.35, 1.0);
            if (Math.Abs(_overlayOpacity - clamped) < 0.001)
                return;

            _overlayOpacity = clamped;
            OnPropertyChanged();
            UpdateOpacityFromCtrlState();
        }
    }

    public bool IsInteractive => IsCtrlDown();

    public MainWindow()
    {
        InitializeComponent();
        ImagesList.ItemsSource = LatestImages;
        TextsList.ItemsSource = LatestTexts;
        DataContext = this;

        _inputTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(40)
        };
        _inputTimer.Tick += InputTimer_Tick;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var handle = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(WndProc);
        AddClipboardFormatListener(handle);
        ApplyCaptureExclusion(handle);

        _inputTimer.Start();
        UpdateClickThroughState(IsCtrlDown());
        UpdateTopmostState(IsCtrlDown());
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmNcHitTest)
        {
            if (!IsCtrlDown())
            {
                handled = true;
                return new IntPtr(-1);
            }

            var hit = HitTestResize(lParam);
            if (hit != HtClient)
            {
                handled = true;
                return new IntPtr(hit);
            }
        }

        if (msg == WmClipboardUpdate)
        {
            RefreshClipboard();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void InputTimer_Tick(object? sender, EventArgs e)
    {
        var ctrlDown = IsCtrlDown();
        OnPropertyChanged(nameof(IsInteractive));
        UpdateClickThroughState(ctrlDown);
        UpdateTopmostState(ctrlDown);

        if (ctrlDown)
        {
            Activate();
            Focus();
        }

        var target = ctrlDown ? CtrlOpacity : OverlayOpacity;
        if (Math.Abs(Opacity - target) > 0.01)
            Opacity = target;
    }

    private static bool IsCtrlDown() => (GetAsyncKeyState(VkControl) & 0x8000) != 0;

    private void RefreshClipboard()
    {
        try
        {
            if (Clipboard.ContainsImage())
            {
                var source = Clipboard.GetImage();
                if (source != null)
                {
                    var entry = ClipboardEntry.FromImage(source);
                    if (entry.Signature == _ignoreNextClipboardSignature)
                    {
                        _ignoreNextClipboardSignature = null;
                        return;
                    }

                    if (TryAddEntry(entry))
                        BringOverlayToFront();
                }

                return;
            }

            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText(TextDataFormat.Text);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var entry = ClipboardEntry.FromText(text.Trim());
                    if (entry.Signature == _ignoreNextClipboardSignature)
                    {
                        _ignoreNextClipboardSignature = null;
                        return;
                    }

                    if (TryAddEntry(entry))
                        BringOverlayToFront();
                }
            }
        }
        catch (Exception)
        {
            // 시작 직후 클립보드 접근이 실패해도 앱 자체는 떠 있어야 합니다.
        }
    }

    private bool TryAddEntry(ClipboardEntry entry)
    {
        if (entry.Signature == _lastClipboardSignature)
            return false;

        _lastClipboardSignature = entry.Signature;

        if (entry.Kind == ClipboardEntryKind.Image)
        {
            LatestImages.Insert(0, entry);
            if (LatestImages.Count > 5)
                LatestImages.RemoveAt(LatestImages.Count - 1);
        }
        else
        {
            LatestTexts.Insert(0, entry);
            if (LatestTexts.Count > 5)
                LatestTexts.RemoveAt(LatestTexts.Count - 1);
        }

        return true;
    }

    private void Item_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!IsCtrlDown())
        {
            e.Handled = true;
            return;
        }

        if (sender is not FrameworkElement element || element.DataContext is not ClipboardEntry entry)
            return;

        SelectItem(element);

        if (_selectedClipboardSignature == entry.Signature)
        {
            IsSelectionExpanded = !IsSelectionExpanded;
            e.Handled = true;
            return;
        }

        SelectedEntry = entry;
        _selectedClipboardSignature = entry.Signature;
        IsSelectionExpanded = true;
        ApplyClipboard(entry);
        e.Handled = true;
    }

    private void ExpandedOverlay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!IsCtrlDown())
        {
            e.Handled = true;
            return;
        }

        IsSelectionExpanded = false;
        e.Handled = true;
    }

    private void SelectItem(DependencyObject current)
    {
        var item = FindAncestor<ListBoxItem>(current);
        if (item is null)
            return;

        item.IsSelected = true;
        item.Focus();
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match)
                return match;

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void HistoryList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is ListBox listBox && listBox.SelectedItem is ClipboardEntry entry && IsCtrlDown())
        {
            if (_selectedClipboardSignature == entry.Signature)
            {
                IsSelectionExpanded = !IsSelectionExpanded;
            }
            else
            {
                SelectedEntry = entry;
                _selectedClipboardSignature = entry.Signature;
                IsSelectionExpanded = true;
                ApplyClipboard(entry);
            }

            e.Handled = true;
        }
    }

    private void ApplyClipboard(ClipboardEntry entry)
    {
        _ignoreNextClipboardSignature = entry.Signature;
        _lastClipboardSignature = entry.Signature;

        if (entry.Kind == ClipboardEntryKind.Image)
        {
            if (entry.ImageSource is BitmapSource bitmapSource)
                Clipboard.SetImage(bitmapSource);
            return;
        }

        Clipboard.SetText(entry.FullText ?? string.Empty);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        DockToRightEdge();
        UpdateOpacityFromCtrlState();
        RefreshClipboard();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
    }

    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsCtrlDown())
            e.Handled = true;
    }

    private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!IsCtrlDown())
            e.Handled = true;
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _inputTimer.Stop();

        if (_source is not null)
            _source.RemoveHook(WndProc);

        var handle = new WindowInteropHelper(this).Handle;
        RemoveClipboardFormatListener(handle);
    }

    private int HitTestResize(IntPtr lParam)
    {
        if (!IsCtrlDown())
            return HtClient;

        var x = unchecked((short)(lParam.ToInt64() & 0xFFFF));
        var y = unchecked((short)((lParam.ToInt64() >> 16) & 0xFFFF));
        var screenPoint = new Point(x, y);
        var windowPoint = PointFromScreen(screenPoint);

        var left = windowPoint.X <= ResizeEdgeSize;
        var right = windowPoint.X >= ActualWidth - ResizeEdgeSize;
        var top = windowPoint.Y <= ResizeEdgeSize;
        var bottom = windowPoint.Y >= ActualHeight - ResizeEdgeSize;

        if (top && left) return HtTopLeft;
        if (top && right) return HtTopRight;
        if (bottom && left) return HtBottomLeft;
        if (bottom && right) return HtBottomRight;
        if (left) return HtLeft;
        if (right) return HtRight;
        if (top) return HtTop;
        if (bottom) return HtBottom;

        return HtClient;
    }

    private void UpdateOpacityFromCtrlState()
    {
        var target = IsCtrlDown() ? CtrlOpacity : OverlayOpacity;
        if (Math.Abs(Opacity - target) > 0.01)
            Opacity = target;
    }

    private void OpacityDragThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!IsCtrlDown())
        {
            e.Handled = true;
            return;
        }

        if (sender is not FrameworkElement element)
            return;

        var slider = FindAncestor<Slider>(element);
        if (slider is null)
            return;

        var width = Math.Max(1, slider.ActualWidth);
        OverlayOpacity = OverlayOpacity + (e.HorizontalChange / width);
        e.Handled = true;
    }

    private void OpacityGauge_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsCtrlDown() || sender is not FrameworkElement element)
        {
            e.Handled = true;
            return;
        }

        _isOpacityDragging = true;
        element.CaptureMouse();
        e.Handled = true;
    }

    private void OpacityGauge_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isOpacityDragging || !IsCtrlDown() || sender is not FrameworkElement element || e.LeftButton != MouseButtonState.Pressed)
            return;

        UpdateOverlayOpacityFromPosition(element, e.GetPosition(element).X);
        e.Handled = true;
    }

    private void OpacityGauge_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element)
            element.ReleaseMouseCapture();

        _isOpacityDragging = false;
        e.Handled = true;
    }

    private void UpdateOverlayOpacityFromPosition(FrameworkElement element, double x)
    {
        var width = Math.Max(1, element.ActualWidth);
        var ratio = Math.Clamp(x / width, 0.0, 1.0);
        OverlayOpacity = 0.35 + (ratio * 0.65);
    }

    private void ApplyCaptureExclusion(IntPtr handle)
    {
        try
        {
            var success = SetWindowDisplayAffinity(handle, WdaExcludeFromCapture);
            LogStartup($"SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)={(success ? "ok" : "failed")}");

            if (!success)
            {
                var fallback = SetWindowDisplayAffinity(handle, WdaMonitor);
                LogStartup($"SetWindowDisplayAffinity(WDA_MONITOR)={(fallback ? "ok" : "failed")}");
            }
        }
        catch (Exception exception)
        {
            LogStartup($"SetWindowDisplayAffinity(exception)={exception.GetType().Name}: {exception.Message}");
        }
    }

    private static void LogStartup(string message)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glassboard");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "startup.log");
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
        }
        catch
        {
        }
    }

    private void UpdateClickThroughState(bool ctrlDown)
    {
        if (_source is null)
            return;

        var handle = new WindowInteropHelper(this).Handle;
        var style = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        if (ctrlDown)
            style &= ~WsExTransparent;
        else
            style |= WsExTransparent;

        SetWindowLongPtr(handle, GwlExStyle, new IntPtr(style));
        SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoZOrder | SwpFrameChanged);
    }

    private void UpdateTopmostState(bool ctrlDown)
    {
        if (_source is null)
            return;

        var handle = new WindowInteropHelper(this).Handle;
        SetWindowPos(handle, ctrlDown ? HwndTopmost : HwndNotopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    private void DockToRightEdge()
    {
        var workArea = SystemParameters.WorkArea;
        var left = workArea.Right - ActualWidth - DockGap;
        var top = workArea.Top + DockGap;

        if (left < workArea.Left + DockGap)
            left = workArea.Left + DockGap;

        if (top < workArea.Top + DockGap)
            top = workArea.Top + DockGap;

        Left = left;
        Top = top;
    }

    private void BringOverlayToFront()
    {
        if (!IsVisible)
            Show();

        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        Activate();
        Topmost = true;
        Focus();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsCtrlDown())
            return;

        if (e.OriginalSource is DependencyObject source &&
            (FindAncestor<ButtonBase>(source) is not null || FindAncestor<Slider>(source) is not null))
            return;

        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
            return;
        }

        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!IsCtrlDown() || sender is not FrameworkElement element || element.Tag is not string direction)
            return;

        ResizeWindow(direction, e);
    }

    private void ResizeWindow(string direction, DragDeltaEventArgs e)
    {
        var minWidth = MinWidth > 0 ? MinWidth : 380;
        var minHeight = MinHeight > 0 ? MinHeight : 540;
        var left = Left;
        var top = Top;
        var width = double.IsNaN(Width) ? ActualWidth : Width;
        var height = double.IsNaN(Height) ? ActualHeight : Height;

        if (direction.Contains("Right"))
            width = Math.Max(minWidth, width + e.HorizontalChange);

        if (direction.Contains("Left"))
        {
            var proposedWidth = width - e.HorizontalChange;
            if (proposedWidth >= minWidth)
            {
                left += e.HorizontalChange;
                width = proposedWidth;
            }
            else
            {
                left += width - minWidth;
                width = minWidth;
            }
        }

        if (direction.Contains("Bottom"))
            height = Math.Max(minHeight, height + e.VerticalChange);

        if (direction.Contains("Top"))
        {
            var proposedHeight = height - e.VerticalChange;
            if (proposedHeight >= minHeight)
            {
                top += e.VerticalChange;
                height = proposedHeight;
            }
            else
            {
                top += height - minHeight;
                height = minHeight;
            }
        }

        Left = left;
        Top = top;
        Width = width;
        Height = height;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, int uFlags);
}

public enum ClipboardEntryKind
{
    Text,
    Image
}

public sealed class ClipboardEntry
{
    public ClipboardEntryKind Kind { get; init; }
    public string KindLabel => Kind == ClipboardEntryKind.Image ? "이미지" : "텍스트";
    public string Preview { get; init; } = string.Empty;
    public string? FullText { get; init; }
    public ImageSource? ImageSource { get; init; }
    public string Signature { get; init; } = string.Empty;
    public string TimestampText { get; init; } = DateTime.Now.ToString("yyyy-MM-dd tt hh:mm");
    public bool IsImage => Kind == ClipboardEntryKind.Image;
    public bool IsText => Kind == ClipboardEntryKind.Text;
    public ImageSource? Image => ImageSource;

    public static ClipboardEntry FromText(string text)
    {
        var preview = text.Replace("\r", " ").Replace("\n", " ⏎ ");
        if (preview.Length > 180)
            preview = preview[..180] + "…";

        return new ClipboardEntry
        {
            Kind = ClipboardEntryKind.Text,
            Preview = preview,
            FullText = text,
            Signature = $"text:{text}"
        };
    }

    public static ClipboardEntry FromImage(ImageSource source)
    {
        var preview = source is BitmapSource bitmap
            ? $"이미지 {bitmap.PixelWidth}×{bitmap.PixelHeight}"
            : $"이미지 {source.Width:0}×{source.Height:0}";

        return new ClipboardEntry
        {
            Kind = ClipboardEntryKind.Image,
            Preview = preview,
            ImageSource = source,
            Signature = source is BitmapSource bmp
                ? $"image:{bmp.PixelWidth}x{bmp.PixelHeight}:{bmp.Format}"
                : $"image:{source.Width:0}x{source.Height:0}"
        };
    }
}
