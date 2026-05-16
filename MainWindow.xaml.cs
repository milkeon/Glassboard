using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Glassboard;

public partial class MainWindow : Window
{
    private const int WmClipboardUpdate = 0x031D;
    private const int VkControl = 0x11;
    private const double NormalOpacity = 0.72;
    private const double CtrlOpacity = 0.98;
    private const double DockGap = 16;

    private readonly DispatcherTimer _inputTimer;
    private HwndSource? _source;
    private string? _lastClipboardSignature;

    public ObservableCollection<ClipboardEntry> Items { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        HistoryList.ItemsSource = Items;
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

        _inputTimer.Start();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmClipboardUpdate)
        {
            RefreshClipboard();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void InputTimer_Tick(object? sender, EventArgs e)
    {
        UpdateOpacityFromCtrlState();
    }

    private void UpdateOpacityFromCtrlState()
    {
        var target = IsCtrlDown() ? CtrlOpacity : NormalOpacity;
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
                    if (TryAddEntry(entry))
                    {
                        StatusText.Text = $"새 이미지 항목을 저장했습니다 · {Items.Count}개";
                        BringOverlayToFront();
                    }
                }

                return;
            }

            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText(TextDataFormat.Text);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var entry = ClipboardEntry.FromText(text.Trim());
                    if (TryAddEntry(entry))
                    {
                        StatusText.Text = $"새 텍스트 항목을 저장했습니다 · {Items.Count}개";
                        BringOverlayToFront();
                    }
                }
            }
        }
        catch (ExternalException)
        {
            // 클립보드가 잠겨 있으면 조용히 넘깁니다.
        }
    }

    private bool TryAddEntry(ClipboardEntry entry)
    {
        if (entry.Signature == _lastClipboardSignature)
            return false;

        _lastClipboardSignature = entry.Signature;
        Items.Insert(0, entry);
        if (Items.Count > 50)
            Items.RemoveAt(Items.Count - 1);

        HistoryList.SelectedItem = entry;
        HistoryList.ScrollIntoView(entry);
        return true;
    }

    private void HistoryList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (HistoryList.SelectedItem is not ClipboardEntry entry)
            return;

        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            ApplyClipboard(entry);
            StatusText.Text = $"고정 선택: {entry.KindLabel}";
        }
        else
        {
            StatusText.Text = $"선택됨: {entry.KindLabel}";
        }
    }

    private void HistoryList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && HistoryList.SelectedItem is ClipboardEntry entry)
        {
            ApplyClipboard(entry);
            StatusText.Text = $"Enter로 복사: {entry.KindLabel}";
            e.Handled = true;
        }
    }

    private static void ApplyClipboard(ClipboardEntry entry)
    {
        if (entry.Kind == ClipboardEntryKind.Image && entry.ImageSource is BitmapSource bitmapSource)
        {
            Clipboard.SetImage(bitmapSource);
            return;
        }

        Clipboard.SetText(entry.FullText ?? string.Empty);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "클립보드를 기다리는 중입니다.";
        DockToRightEdge();
        UpdateOpacityFromCtrlState();
        RefreshClipboard();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (WindowState == WindowState.Normal)
            DockToRightEdge();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _inputTimer.Stop();

        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
        }

        var handle = new WindowInteropHelper(this).Handle;
        RemoveClipboardFormatListener(handle);
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
        DockToRightEdge();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
            return;
        }

        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
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
