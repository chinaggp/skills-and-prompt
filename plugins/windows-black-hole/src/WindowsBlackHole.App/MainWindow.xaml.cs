using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WindowsBlackHole.Core;
using WindowsBlackHole.Shell;
using Point = System.Windows.Point;

namespace WindowsBlackHole.App;

public partial class MainWindow : Window
{
    private readonly RecycleBinService _recycleBinService = new();
    private readonly DispatcherTimer _cursorTimer;
    private readonly Stopwatch _animationClock = Stopwatch.StartNew();
    private bool _isFileDragActive;

    public MainWindow()
    {
        InitializeComponent();

        _cursorTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(16),
            DispatcherPriority.Render,
            OnAnimationTick,
            Dispatcher);

        Loaded += (_, _) =>
        {
            PositionAtBottomRight();
            _cursorTimer.Start();
        };
        Closed += (_, _) => _cursorTimer.Stop();
    }

    private void PositionAtBottomRight()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 24;
        Top = workArea.Bottom - Height - 24;
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        if (!GetCursorPos(out var cursor))
        {
            return;
        }

        var local = PointFromScreen(new Point(cursor.X, cursor.Y));
        var proximity = BlackHoleGeometry.Proximity(local.X, local.Y);
        if (_isFileDragActive)
        {
            proximity = Math.Max(proximity, 0.78);
        }

        var elapsed = _animationClock.Elapsed.TotalSeconds;
        FieldRotation.Angle = (elapsed * (8 + (proximity * 38))) % 360;
        FieldScale.ScaleX = 1 + (proximity * 0.08);
        FieldScale.ScaleY = 1 - (proximity * 0.055);
        FieldSkew.AngleX = Math.Sin(elapsed * 3.4) * proximity * 7;
        FieldSkew.AngleY = Math.Cos(elapsed * 2.7) * proximity * 4;
        LensGlow.Opacity = 0.36 + (proximity * 0.58);
    }

    private void OnEventHorizonMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // 鼠标状态在 DragMove 建立前发生变化时，WPF 会抛出此异常。
        }
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        _isFileDragActive = e.Data.GetDataPresent(DataFormats.FileDrop);
        UpdateDragEffect(e);
    }

    private void OnDragOver(object sender, DragEventArgs e) => UpdateDragEffect(e);

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        _isFileDragActive = false;
        HideStatus();
    }

    private void UpdateDragEffect(DragEventArgs e)
    {
        var position = e.GetPosition(RootCanvas);
        var hasFiles = e.Data.GetDataPresent(DataFormats.FileDrop);
        var isInside = BlackHoleGeometry.IsInsideEventHorizon(position.X, position.Y);

        e.Effects = hasFiles && isInside ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;

        if (hasFiles)
        {
            ShowStatus(
                isInside ? "松开后移入回收站" : "把文件放进黑洞中心",
                isInside ? "#FFF7EBDD" : "#CCFFFFFF");
        }
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        _isFileDragActive = false;
        var position = e.GetPosition(RootCanvas);

        if (!BlackHoleGeometry.IsInsideEventHorizon(position.X, position.Y) ||
            !e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            ShowStatus("没有吞噬：请放进黑洞中心", "#FFFFBE7A");
            return;
        }

        var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
        var validation = DropValidator.Validate(paths ?? Array.Empty<string>());
        if (!validation.IsValid)
        {
            ShowFailure(validation.Error ?? "文件验证失败。");
            return;
        }

        try
        {
            var windowHandle = new WindowInteropHelper(this).Handle;
            _recycleBinService.MoveToRecycleBin(validation.Paths, windowHandle);
            PlayConsumeAnimation();
            ShowStatus($"已移入回收站：{validation.Paths.Count} 项", "#FFFFFFFF");
        }
        catch (Exception exception)
        {
            ShowFailure(exception.Message);
        }
    }

    private void PlayConsumeAnimation()
    {
        var scaleAnimation = new DoubleAnimation(1.18, 0.72, TimeSpan.FromMilliseconds(210))
        {
            AutoReverse = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
        };
        EventHorizon.RenderTransformOrigin = new Point(0.5, 0.5);
        EventHorizon.RenderTransform = new ScaleTransform();
        EventHorizon.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
        EventHorizon.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);

        DropPulse.Stroke = new SolidColorBrush(Color.FromRgb(255, 244, 225));
        DropPulse.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(520)));
    }

    private void ShowFailure(string message)
    {
        DropPulse.Stroke = new SolidColorBrush(Color.FromRgb(255, 82, 82));
        DropPulse.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(700)));
        ShowStatus(message, "#FFFF8A80");
    }

    private void ShowStatus(string message, string color)
    {
        StatusText.Text = message;
        StatusText.Foreground = (Brush)new BrushConverter().ConvertFromString(color)!;
        StatusBadge.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0.94, TimeSpan.FromMilliseconds(100)));

        var hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        hideTimer.Tick += (_, _) =>
        {
            hideTimer.Stop();
            HideStatus();
        };
        hideTimer.Start();
    }

    private void HideStatus() =>
        StatusBadge.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0, TimeSpan.FromMilliseconds(240)));

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}
