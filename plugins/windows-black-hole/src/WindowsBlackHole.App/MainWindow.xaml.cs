using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsBlackHole.App.Rendering;
using WindowsBlackHole.Core;
using WindowsBlackHole.Shell;
using Point = System.Windows.Point;

namespace WindowsBlackHole.App;

public partial class MainWindow : Window
{
    private readonly RecycleBinService _recycleBinService = new();
    private readonly NativeRendererHost _nativeRenderer = new();
    private readonly DispatcherTimer _cursorTimer;
    private readonly Stopwatch _animationClock = Stopwatch.StartNew();
    private readonly bool _freezeForDiagnostics;
    private VisualFeedbackState _feedbackState;
    private bool _isFileDragActive;
    private double _feedbackStartedAt;
    private double _lastTickSeconds;
    private double _statusTargetOpacity;
    private double _statusVisibleUntil;
    private nint _windowHandle;
    private bool _nativeWindowsStacked;

    public MainWindow(bool freezeForDiagnostics = false)
    {
        _freezeForDiagnostics = freezeForDiagnostics;
        InitializeComponent();

        _cursorTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(16),
            DispatcherPriority.Render,
            OnAnimationTick,
            Dispatcher);

        Loaded += (_, _) =>
        {
            PositionAtBottomRight();
            InitializeNativeRenderer();
            _cursorTimer.Start();
            if (_freezeForDiagnostics)
            {
                ScheduleDiagnosticFreeze();
            }
        };
        LocationChanged += (_, _) => UpdateNativeRenderer(0);
        SizeChanged += (_, _) => UpdateNativeRenderer(0);
        Closed += (_, _) =>
        {
            _cursorTimer.Stop();
            _nativeRenderer.Dispose();
        };
    }

    private void PositionAtBottomRight()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 24;
        Top = workArea.Bottom - Height - 24;
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        var elapsed = _animationClock.Elapsed.TotalSeconds;
        var delta = Math.Clamp(elapsed - _lastTickSeconds, 0, 0.1);
        _lastTickSeconds = elapsed;

        var proximity = 0d;
        if (GetCursorPos(out var cursor))
        {
            var local = PointFromScreen(new Point(cursor.X, cursor.Y));
            proximity = BlackHoleGeometry.Proximity(local.X, local.Y);
        }

        if (_isFileDragActive)
        {
            proximity = Math.Max(
                proximity,
                _feedbackState == VisualFeedbackState.DragTarget ? 1 : 0.72);
        }

        var breath = (Math.Sin(elapsed * 1.65) + 1) * 0.5;
        var orbitalSpeed = 1.4 + (proximity * 3.2);

        // Keep the cinematic diagonal stable while the individual layers drift.
        FieldRotation.Angle = -1.8 + (Math.Sin(elapsed * 0.34) * (1.2 + proximity));
        FieldScale.ScaleX = 1 + (proximity * 0.055) + (breath * 0.006);
        FieldScale.ScaleY = 1 - (proximity * 0.036) + (breath * 0.004);
        FieldSkew.AngleX = Math.Sin(elapsed * 0.72) * (0.8 + (proximity * 2.4));
        FieldSkew.AngleY = Math.Cos(elapsed * 0.58) * proximity * 1.4;

        ForegroundRotation.Angle = -0.8 + (Math.Sin(elapsed * 0.42) * (0.7 + proximity));
        ForegroundScale.ScaleX = 1 + (proximity * 0.075);
        ForegroundScale.ScaleY = 1 - (proximity * 0.025);
        ForegroundSkew.AngleX = Math.Sin(elapsed * 0.86) * proximity * 2.2;

        GridRotation.Angle = Math.Sin(elapsed * 0.17) * 1.8;
        GridScale.ScaleX = 1 - (proximity * 0.045);
        GridScale.ScaleY = 1 - (proximity * 0.045);
        GridSkew.AngleX = Math.Sin(elapsed * 0.47) * proximity * 2.8;
        GridSkew.AngleY = Math.Cos(elapsed * 0.39) * proximity * 1.8;
        GridLayer.Opacity = 0.34 + (proximity * 0.24);

        DustRotation.Angle = (elapsed * orbitalSpeed) % 360;
        DustTranslation.X = Math.Sin(elapsed * 0.23) * 2.4;
        DustTranslation.Y = Math.Cos(elapsed * 0.19) * 1.8;
        DustLayer.Opacity = 0.48 + (breath * 0.14) + (proximity * 0.08);

        MatterStreamScale.ScaleX = 1 + (proximity * 0.14);
        MatterStreamScale.ScaleY = 1 - (proximity * 0.08);
        MatterStreamSkew.AngleX = Math.Sin(elapsed * 1.9) * (1 + (proximity * 4));

        LensGlow.Opacity = 0.34 + (proximity * 0.48) + (breath * 0.08);
        LensGlowParticleRotation.Angle = -((elapsed * 0.045) % 360);
        LensGlowParticleTranslation.X = Math.Sin(elapsed * 0.055) * 0.85;
        LensGlowParticleTranslation.Y = Math.Cos(elapsed * 0.041) * 0.6;
        PhotonHaloOuterRotation.Angle = (elapsed * 0.16) % 360;
        PhotonHaloOuterTranslation.X = Math.Sin(elapsed * 0.11) * 1.1;
        PhotonHaloOuterTranslation.Y = Math.Cos(elapsed * 0.08) * 0.7;
        PhotonHaloInnerRotation.Angle = -((elapsed * 0.11) % 360);
        PhotonHaloInnerTranslation.X = Math.Cos(elapsed * 0.09) * 0.65;
        PhotonHaloInnerTranslation.Y = Math.Sin(elapsed * 0.13) * 0.95;
        PhotonHaloSparkRotation.Angle = (elapsed * 0.19) % 360;
        PhotonHaloSparkTranslation.X = Math.Sin(elapsed * 0.07) * 0.5;
        PhotonHaloSparkTranslation.Y = Math.Cos(elapsed * 0.15) * 0.8;
        PhotonHalo.Opacity = 0.48 + (proximity * 0.4) + (breath * 0.08);
        PhotonRing.Opacity = 0.76 + (proximity * 0.22);
        PhotonRingParticleRotation.Angle = (elapsed * 0.072) % 360;
        PhotonRingParticleTranslation.X = Math.Cos(elapsed * 0.063) * 0.45;
        PhotonRingParticleTranslation.Y = Math.Sin(elapsed * 0.049) * 0.7;

        UpdateFeedbackAnimation(elapsed);
        UpdateStatusOpacity(elapsed, delta);
        UpdateNativeRenderer(proximity);
    }

    private void UpdateFeedbackAnimation(double elapsed)
    {
        CoreScale.ScaleX = 1;
        CoreScale.ScaleY = 1;
        SuccessFlash.Opacity = 0;
        FailureHalo.Opacity = 0;
        DropPulse.Opacity = 0;
        DropPulseScale.ScaleX = 1;
        DropPulseScale.ScaleY = 1;

        switch (_feedbackState)
        {
            case VisualFeedbackState.DragOutside:
                PhotonRing.Opacity = Math.Max(PhotonRing.Opacity, 0.72);
                break;

            case VisualFeedbackState.DragInfluence:
            {
                var pulse = (Math.Sin(elapsed * 5.2) + 1) * 0.5;
                DropPulse.Opacity = 0.1 + (pulse * 0.18);
                DropPulseScale.ScaleX = 1.08 + (pulse * 0.08);
                DropPulseScale.ScaleY = DropPulseScale.ScaleX;
                PhotonHalo.Opacity = Math.Max(PhotonHalo.Opacity, 0.82);
                break;
            }

            case VisualFeedbackState.DragTarget:
            {
                var pulse = (Math.Sin(elapsed * 7.4) + 1) * 0.5;
                DropPulse.Opacity = 0.35 + (pulse * 0.32);
                DropPulseScale.ScaleX = 0.98 + (pulse * 0.08);
                DropPulseScale.ScaleY = DropPulseScale.ScaleX;
                PhotonRing.Opacity = 1;
                PhotonHalo.Opacity = 1;
                break;
            }

            case VisualFeedbackState.ConsumeSuccess:
            {
                const double duration = 0.52;
                var progress = Math.Clamp((elapsed - _feedbackStartedAt) / duration, 0, 1);
                var collapse = Math.Sin(progress * Math.PI);
                var shock = EaseOutCubic(progress);

                CoreScale.ScaleX = 1 - (collapse * 0.38);
                CoreScale.ScaleY = CoreScale.ScaleX;
                SuccessFlash.Opacity = Math.Max(0, 1 - (progress / 0.36)) * 0.94;
                DropPulse.Opacity = 1 - progress;
                DropPulseScale.ScaleX = 0.72 + (shock * 1.28);
                DropPulseScale.ScaleY = DropPulseScale.ScaleX;
                PhotonHalo.Opacity = 1;
                PhotonRing.Opacity = 1;

                if (progress >= 1)
                {
                    _feedbackState = VisualFeedbackState.Idle;
                }

                break;
            }

            case VisualFeedbackState.Failure:
            {
                const double duration = 0.7;
                var progress = Math.Clamp((elapsed - _feedbackStartedAt) / duration, 0, 1);
                var pulse = Math.Sin(progress * Math.PI * 3) * (1 - progress);

                CoreScale.ScaleX = 1 + (pulse * 0.035);
                CoreScale.ScaleY = CoreScale.ScaleX;
                FailureHalo.Opacity = Math.Max(0, pulse) * 0.92;
                DropPulse.Opacity = (1 - progress) * 0.78;
                DropPulseScale.ScaleX = 0.95 + (progress * 0.42);
                DropPulseScale.ScaleY = DropPulseScale.ScaleX;

                if (progress >= 1)
                {
                    _feedbackState = VisualFeedbackState.Idle;
                }

                break;
            }
        }
    }

    private void UpdateStatusOpacity(double elapsed, double delta)
    {
        if (!_isFileDragActive &&
            _statusTargetOpacity > 0 &&
            elapsed >= _statusVisibleUntil)
        {
            _statusTargetOpacity = 0;
        }

        var blend = 1 - Math.Exp(-delta * (_statusTargetOpacity > 0 ? 17 : 8));
        StatusBadge.Opacity += (_statusTargetOpacity - StatusBadge.Opacity) * blend;
    }

    private static double EaseOutCubic(double value)
    {
        var inverse = 1 - value;
        return 1 - (inverse * inverse * inverse);
    }

    private void InitializeNativeRenderer()
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
        _ = SetWindowDisplayAffinity(_windowHandle, DisplayAffinityExcludeFromCapture);

        var dpi = VisualTreeHelper.GetDpi(this);
        var width = (uint)Math.Max(1, Math.Round(ActualWidth * dpi.DpiScaleX));
        var height = (uint)Math.Max(1, Math.Round(ActualHeight * dpi.DpiScaleY));
        if (_nativeRenderer.TryInitialize(width, height, (float)dpi.DpiScaleX))
        {
            UpdateNativeRenderer(0);
        }
    }

    private void ScheduleDiagnosticFreeze()
    {
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2),
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            UpdateNativeRenderer(1);
            if (_nativeRenderer.IsCaptureActive &&
                _nativeRenderer.FreezeForDiagnostics())
            {
                _cursorTimer.Stop();
                _ = SetWindowDisplayAffinity(_windowHandle, 0);
                Title = "Windows Black Hole [Frozen Lens]";
            }
            else
            {
                Title = "Windows Black Hole [Capture Failed]";
            }
        };
        timer.Start();
    }

    private void UpdateNativeRenderer(double strength)
    {
        if (!_nativeRenderer.IsInitialized || !IsLoaded)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var origin = PointToScreen(new Point(0, 0));
        var width = (uint)Math.Max(1, Math.Round(ActualWidth * dpi.DpiScaleX));
        var height = (uint)Math.Max(1, Math.Round(ActualHeight * dpi.DpiScaleY));

        _ = _nativeRenderer.Update(
            (int)Math.Round(origin.X),
            (int)Math.Round(origin.Y),
            width,
            height,
            (float)strength);

        var overlayWindow = _nativeRenderer.OverlayWindow;
        if (!_nativeWindowsStacked &&
            overlayWindow != nint.Zero &&
            _windowHandle != nint.Zero)
        {
            var overlayPositioned = SetWindowPos(
                overlayWindow,
                HwndTopmost,
                0,
                0,
                0,
                0,
                SetWindowPositionNoMove |
                SetWindowPositionNoSize |
                SetWindowPositionNoActivate);
            var appPositioned = SetWindowPos(
                _windowHandle,
                HwndTopmost,
                0,
                0,
                0,
                0,
                SetWindowPositionNoMove |
                SetWindowPositionNoSize |
                SetWindowPositionNoActivate);
            _nativeWindowsStacked = overlayPositioned && appPositioned;
        }
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
        HideFileDragVisual();
        SetFeedbackState(VisualFeedbackState.Idle);
        HideStatus();
    }

    private void UpdateDragEffect(DragEventArgs e)
    {
        var position = e.GetPosition(RootCanvas);
        var hasFiles = e.Data.GetDataPresent(DataFormats.FileDrop);
        var isInside = BlackHoleGeometry.IsInsideEventHorizon(position.X, position.Y);
        var proximity = BlackHoleGeometry.Proximity(position.X, position.Y);
        _isFileDragActive = hasFiles;

        e.Effects = hasFiles && isInside ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;

        if (!hasFiles)
        {
            HideFileDragVisual();
            SetFeedbackState(VisualFeedbackState.Idle);
            HideStatus();
            return;
        }

        UpdateFileDragVisual(position, proximity, e.Data);

        if (isInside)
        {
            SetFeedbackState(VisualFeedbackState.DragTarget);
            ShowStatus(
                "RELEASE TO CONSUME",
                "松开后移入回收站",
                "#FFFFF3DD",
                persistWhileDragging: true);
        }
        else if (proximity > 0)
        {
            SetFeedbackState(VisualFeedbackState.DragInfluence);
            ShowStatus(
                "GRAVITY LOCK",
                "继续拖向事件视界中心",
                "#FFFFC27D",
                persistWhileDragging: true);
        }
        else
        {
            SetFeedbackState(VisualFeedbackState.DragOutside);
            ShowStatus(
                "NO GRAVITY CONTACT",
                "文件尚未进入引力范围",
                "#CCD8E2E8",
                persistWhileDragging: true);
        }
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        _isFileDragActive = false;
        HideFileDragVisual();
        var position = e.GetPosition(RootCanvas);

        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            SetFeedbackState(VisualFeedbackState.Idle);
            HideStatus();
            return;
        }

        if (!BlackHoleGeometry.IsInsideEventHorizon(position.X, position.Y))
        {
            ShowFailure("没有吞噬：请放进黑洞中心");
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
            ShowStatus(
                "CONSUMED / RECYCLED",
                $"已移入回收站：{validation.Paths.Count} 项",
                "#FFFFFFFF");
        }
        catch (Exception exception)
        {
            ShowFailure(exception.Message);
        }
    }

    private void UpdateFileDragVisual(Point position, double proximity, IDataObject data)
    {
        if (proximity <= 0)
        {
            HideFileDragVisual();
            return;
        }

        var transform = BlackHoleGeometry.DragLensTransform(position.X, position.Y);
        Canvas.SetLeft(FileDragProxy, transform.X - (FileDragProxy.Width / 2));
        Canvas.SetTop(FileDragProxy, transform.Y - (FileDragProxy.Height / 2));
        FileDragProxyScale.ScaleX = transform.ScaleX;
        FileDragProxyScale.ScaleY = transform.ScaleY;
        FileDragProxySkew.AngleY = transform.SkewDegrees;
        FileDragProxyRotation.Angle = transform.RotationDegrees;
        FileDragProxy.Opacity = transform.Opacity;

        DragGravityTrail.X1 = position.X;
        DragGravityTrail.Y1 = position.Y;
        DragGravityTrail.X2 = transform.X;
        DragGravityTrail.Y2 = transform.Y;
        DragGravityTrail.StrokeThickness = 1.2 + (proximity * 5.5);
        DragGravityTrail.Opacity = transform.Opacity * 0.78;

        if (data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
        {
            FileDragExtension.Text = "FILE";
            FileDragCount.Text = "1 ITEM";
            return;
        }

        var extension = Path.GetExtension(paths[0]).TrimStart('.').ToUpperInvariant();
        FileDragExtension.Text = string.IsNullOrWhiteSpace(extension)
            ? "FOLDER"
            : extension[..Math.Min(extension.Length, 6)];
        FileDragCount.Text = paths.Length == 1 ? "1 ITEM" : $"+{paths.Length - 1} MORE";
    }

    private void HideFileDragVisual()
    {
        FileDragProxy.Opacity = 0;
        DragGravityTrail.Opacity = 0;
    }

    private void PlayConsumeAnimation()
    {
        DropPulse.Stroke = new SolidColorBrush(Color.FromRgb(255, 244, 223));
        SetFeedbackState(VisualFeedbackState.ConsumeSuccess);
    }

    private void ShowFailure(string message)
    {
        DropPulse.Stroke = new SolidColorBrush(Color.FromRgb(255, 91, 50));
        SetFeedbackState(VisualFeedbackState.Failure);
        ShowStatus(
            "GRAVITY REJECTED",
            CompactMessage(message),
            "#FFFF8A72");
    }

    private void SetFeedbackState(VisualFeedbackState state)
    {
        if (_feedbackState == state)
        {
            return;
        }

        _feedbackState = state;
        _feedbackStartedAt = _animationClock.Elapsed.TotalSeconds;
        if (state is VisualFeedbackState.DragInfluence or VisualFeedbackState.DragTarget)
        {
            DropPulse.Stroke = new SolidColorBrush(
                state == VisualFeedbackState.DragTarget
                    ? Color.FromRgb(255, 247, 230)
                    : Color.FromRgb(255, 177, 96));
        }
    }

    private void ShowStatus(
        string label,
        string message,
        string color,
        bool persistWhileDragging = false)
    {
        StatusLabel.Text = label;
        StatusText.Text = message;
        var brush = (Brush)new BrushConverter().ConvertFromString(color)!;
        StatusLabel.Foreground = brush;
        StatusText.Foreground = brush;
        _statusTargetOpacity = 0.96;
        _statusVisibleUntil = persistWhileDragging
            ? double.PositiveInfinity
            : _animationClock.Elapsed.TotalSeconds + 2.5;
    }

    private void HideStatus() => _statusTargetOpacity = 0;

    private static string CompactMessage(string message)
    {
        const int maximumLength = 82;
        var compact = string.Join(
            " ",
            message.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return compact.Length <= maximumLength
            ? compact
            : $"{compact[..(maximumLength - 1)]}…";
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(nint window, uint affinity);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint window,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    private static readonly nint HwndTopmost = new(-1);
    private const uint DisplayAffinityExcludeFromCapture = 0x00000011;
    private const uint SetWindowPositionNoSize = 0x0001;
    private const uint SetWindowPositionNoMove = 0x0002;
    private const uint SetWindowPositionNoActivate = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    private enum VisualFeedbackState
    {
        Idle,
        DragOutside,
        DragInfluence,
        DragTarget,
        ConsumeSuccess,
        Failure,
    }
}
