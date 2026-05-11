using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using TypeWhisper.Core.Models;

namespace TypeWhisper.Windows.Controls.FloatingMicButton;

public enum MicButtonState { Idle, Loading, Recording, Done }

public partial class FloatingMicButtonWindow : Window
{
    private const int GWL_EXSTYLE      = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [System.Runtime.InteropServices.LibraryImport("user32.dll")]
    private static partial int GetWindowLongW(IntPtr hWnd, int nIndex);

    [System.Runtime.InteropServices.LibraryImport("user32.dll")]
    private static partial int SetWindowLongW(IntPtr hWnd, int nIndex, int dwNewLong);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd    = new WindowInteropHelper(this).Handle;
        var exStyle = GetWindowLongW(hwnd, GWL_EXSTYLE);
        SetWindowLongW(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE);
    }

    private readonly FloatingMicButtonCorner _corner;
    private Point _mouseDownPos;
    private bool _wasDrag;
    private CancellationTokenSource? _doneCts;
    private CancellationTokenSource? _sonarCts;

    public event EventHandler? ButtonDown;
    public event EventHandler? ButtonUp;
    public event EventHandler? ButtonCancelled;

    public FloatingMicButtonWindow(FloatingMicButtonCorner corner, int buttonSize)
    {
        InitializeComponent();
        _corner = corner;

        ButtonBorder.Width = buttonSize;
        ButtonBorder.Height = buttonSize;
        ButtonIcon.FontSize = buttonSize * 0.38;

        Loaded += (_, _) =>
        {
            PositionAtCorner();
            ApplyRippleClip();
        };

        PreviewMouseLeftButtonDown += OnMouseDown;
        PreviewMouseMove += OnMouseMove;
        PreviewMouseLeftButtonUp += OnMouseUp;
        TouchDown += OnTouchDown;
        TouchUp += OnTouchUp;
    }

    public void SetState(MicButtonState state)
    {
        _doneCts?.Cancel();
        _doneCts = null;
        StopPulse();
        StopSonar();

        switch (state)
        {
            case MicButtonState.Idle:
                ButtonBackground.Color = Color.FromRgb(0x2D, 0x2D, 0x42);
                ButtonIcon.Text = "";
                ButtonIcon.Foreground = new SolidColorBrush(Color.FromArgb(0xAA, 0xDD, 0xDD, 0xDD));
                break;

            case MicButtonState.Loading:
                ButtonBackground.Color = Color.FromRgb(0xF7, 0x97, 0x00);
                ButtonIcon.Text = "\uE916"; // Hourglass
                ButtonIcon.Foreground = new SolidColorBrush(Colors.White);
                StartPulse();
                break;

            case MicButtonState.Recording:
                ButtonBackground.Color = Color.FromRgb(0xE8, 0x11, 0x23);
                ButtonIcon.Text = "";
                ButtonIcon.Foreground = new SolidColorBrush(Colors.White);
                StartPulse();
                StartSonar();
                break;

            case MicButtonState.Done:
                ButtonBackground.Color = Color.FromRgb(0x22, 0x8B, 0x22);
                ButtonIcon.Text = "";
                ButtonIcon.Foreground = new SolidColorBrush(Colors.White);
                _doneCts = new CancellationTokenSource();
                var token = _doneCts.Token;
                _ = Task.Delay(2000, token).ContinueWith(_ =>
                {
                    if (!token.IsCancellationRequested)
                        Dispatcher.InvokeAsync(() => SetState(MicButtonState.Idle));
                }, TaskContinuationOptions.OnlyOnRanToCompletion);
                break;
        }
    }

    private void PositionAtCorner()
    {
        // SystemParameters.WorkArea reflects the primary monitor only; multi-monitor not supported
        var workArea = SystemParameters.WorkArea;
        const double margin = 20;
        Left = _corner is FloatingMicButtonCorner.TopLeft or FloatingMicButtonCorner.BottomLeft
            ? workArea.Left + margin
            : workArea.Right - ActualWidth - margin;
        Top = _corner is FloatingMicButtonCorner.TopLeft or FloatingMicButtonCorner.TopRight
            ? workArea.Top + margin
            : workArea.Bottom - ActualHeight - margin;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _mouseDownPos = e.GetPosition(this);
        _wasDrag = false;
        Mouse.Capture(this);
        ButtonDown?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
        SpawnPressRipple(e.GetPosition(RippleCanvas));
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!IsMouseCaptured || _wasDrag) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var pos = e.GetPosition(this);
        if (Math.Abs(pos.X - _mouseDownPos.X) > 8 || Math.Abs(pos.Y - _mouseDownPos.Y) > 8)
        {
            _wasDrag = true;
            Mouse.Capture(null);
            ButtonCancelled?.Invoke(this, EventArgs.Empty);
            DragMove();

            ButtonUp?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        Mouse.Capture(null);
        if (!_wasDrag)
            ButtonUp?.Invoke(this, EventArgs.Empty);
        _wasDrag = false;
        e.Handled = true;
    }

    private void OnTouchDown(object sender, TouchEventArgs e)
    {
        ButtonDown?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void OnTouchUp(object sender, TouchEventArgs e)
    {
        ButtonUp?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void ApplyRippleClip()
    {
        RippleCanvas.Clip = new EllipseGeometry(
            new Point(ButtonBorder.ActualWidth / 2, ButtonBorder.ActualHeight / 2),
            ButtonBorder.ActualWidth / 2,
            ButtonBorder.ActualHeight / 2);
    }

    private void SpawnPressRipple(Point originInButton)
    {
        double diameter = ButtonBorder.ActualWidth * 1.6;

        var circle = new Ellipse
        {
            Width                 = diameter,
            Height                = diameter,
            Fill                  = new SolidColorBrush(Color.FromArgb(55, 255, 255, 255)),
            IsHitTestVisible      = false,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform       = new ScaleTransform(0, 0)
        };
        Canvas.SetLeft(circle, originInButton.X - diameter / 2);
        Canvas.SetTop(circle,  originInButton.Y - diameter / 2);
        RippleCanvas.Children.Add(circle);

        var scale       = (ScaleTransform)circle.RenderTransform;
        var dur         = TimeSpan.FromMilliseconds(450);
        var ease        = new CubicEase { EasingMode = EasingMode.EaseOut };
        var scaleAnim   = new DoubleAnimation(0, 1, dur) { EasingFunction = ease };
        var opacityAnim = new DoubleAnimation(0.85, 0, dur) { EasingFunction = ease };
        opacityAnim.Completed += (_, _) => RippleCanvas.Children.Remove(circle);

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        circle.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
    }

    private void StartSonar()
    {
        _sonarCts = new CancellationTokenSource();
        var token = _sonarCts.Token;
        _ = RunSonarLoopAsync(token);
    }

    private void StopSonar()
    {
        _sonarCts?.Cancel();
        _sonarCts = null;
        SonarCanvas.Children.Clear();
    }

    private async Task RunSonarLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Dispatcher.Invoke(SpawnSonarRing);
            try { await Task.Delay(900, token); }
            catch (TaskCanceledException) { break; }
        }
    }

    private void SpawnSonarRing()
    {
        double d  = ButtonBorder.ActualWidth;
        double cx = SonarCanvas.ActualWidth  / 2;
        double cy = SonarCanvas.ActualHeight / 2;

        var ring = new Ellipse
        {
            Width           = d,
            Height          = d,
            Stroke          = new SolidColorBrush(Color.FromArgb(180, 232, 17, 35)),
            StrokeThickness = 2,
            Fill            = Brushes.Transparent,
            IsHitTestVisible = false,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(1, 1)
        };
        Canvas.SetLeft(ring, cx - d / 2);
        Canvas.SetTop(ring,  cy - d / 2);
        SonarCanvas.Children.Add(ring);

        double endScale = 1 + 52.0 / d;
        var scale = (ScaleTransform)ring.RenderTransform;
        var dur   = TimeSpan.FromMilliseconds(900);
        var ease  = new CubicEase { EasingMode = EasingMode.EaseOut };

        var sAnim = new DoubleAnimation(1, endScale, dur) { EasingFunction = ease };
        var oAnim = new DoubleAnimation(0.85, 0, dur)    { EasingFunction = ease };
        oAnim.Completed += (_, _) => SonarCanvas.Children.Remove(ring);

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, sAnim);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, sAnim);
        ring.BeginAnimation(UIElement.OpacityProperty, oAnim);
    }

    private void StartPulse()
    {
        var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
        var anim = new DoubleAnimation(1.0, 1.09, TimeSpan.FromSeconds(0.65))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = ease
        };
        PulseScale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        PulseScale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
    }

    private void StopPulse()
    {
        PulseScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        PulseScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
    }
}
