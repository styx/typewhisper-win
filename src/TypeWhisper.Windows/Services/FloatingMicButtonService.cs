using System.Windows;
using TypeWhisper.Core.Interfaces;
using TypeWhisper.Core.Models;
using TypeWhisper.PluginSDK.Models;
using TypeWhisper.Windows.Controls.FloatingMicButton;
using TypeWhisper.Windows.Services.Plugins;
using TypeWhisper.Windows.ViewModels;

namespace TypeWhisper.Windows.Services;

public sealed class FloatingMicButtonService : IDisposable
{
    private readonly DictationViewModel _dictation;
    private readonly PluginEventBus _eventBus;
    private readonly ISettingsService _settings;
    private readonly TextInsertionService _insertion;
    private FloatingMicButtonWindow? _window;
    private IDisposable? _recordingStartedSub;
    private IDisposable? _transcriptionCompletedSub;
    private IDisposable? _transcriptionFailedSub;
    private bool _buttonWasDragged;

    public FloatingMicButtonService(
        DictationViewModel dictation,
        PluginEventBus eventBus,
        ISettingsService settings,
        TextInsertionService insertion)
    {
        _dictation  = dictation;
        _eventBus   = eventBus;
        _settings   = settings;
        _insertion  = insertion;
        _settings.SettingsChanged += OnSettingsChanged;
    }

    public void Initialize()
    {
        Application.Current.Dispatcher.InvokeAsync(() => Apply(_settings.Current));
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        Application.Current.Dispatcher.InvokeAsync(() => Apply(settings));
    }

    private void Apply(AppSettings settings)
    {
        if (settings.FloatingMicButtonEnabled)
            ShowWindow(settings);
        else
            CloseWindow();
    }

    private void ShowWindow(AppSettings settings)
    {
        CloseWindow();

        _window = new FloatingMicButtonWindow(settings.FloatingMicButtonCorner, settings.FloatingMicButtonSize);
        _window.ButtonDown += OnButtonDown;
        _window.ButtonUp += OnButtonUp;
        _window.ButtonCancelled += OnButtonCancelled;

        _recordingStartedSub = _eventBus.Subscribe<RecordingStartedEvent>(_ =>
        {
            Application.Current.Dispatcher.InvokeAsync(() => _window?.SetState(MicButtonState.Recording));
            return Task.CompletedTask;
        });

        _transcriptionCompletedSub = _eventBus.Subscribe<TranscriptionCompletedEvent>(e =>
        {
            Application.Current.Dispatcher.InvokeAsync(() => _window?.SetState(
                string.IsNullOrWhiteSpace(e.Text) ? MicButtonState.Idle : MicButtonState.Done));
            if (!string.IsNullOrWhiteSpace(e.Text))
                _ = _insertion.InsertTextAsync(e.Text, autoPaste: _settings.Current.AutoPaste);
            return Task.CompletedTask;
        });

        _transcriptionFailedSub = _eventBus.Subscribe<TranscriptionFailedEvent>(_ =>
        {
            Application.Current.Dispatcher.InvokeAsync(() => _window?.SetState(MicButtonState.Idle));
            return Task.CompletedTask;
        });

        _window.Show();
    }

    private void CloseWindow()
    {
        _recordingStartedSub?.Dispose();
        _transcriptionCompletedSub?.Dispose();
        _transcriptionFailedSub?.Dispose();
        _recordingStartedSub = _transcriptionCompletedSub = _transcriptionFailedSub = null;

        if (_window is not null)
        {
            _window.ButtonDown -= OnButtonDown;
            _window.ButtonUp -= OnButtonUp;
            _window.ButtonCancelled -= OnButtonCancelled;
            _window.Close();
            _window = null;
        }
    }

    private void OnButtonDown(object? sender, EventArgs e)
    {
        if (_settings.Current.FloatingMicButtonMode != FloatingMicButtonMode.Hold)
            return;
        if (!_dictation.IsRecording)
            _ = _dictation.StartRecordingAsync();
    }

    private void OnButtonUp(object? sender, EventArgs e)
    {
        var wasDrag = _buttonWasDragged;
        _buttonWasDragged = false;

        var mode = _settings.Current.FloatingMicButtonMode;
        if (mode == FloatingMicButtonMode.Hold)
        {
            if (_dictation.IsRecording)
                _ = _dictation.StopRecordingAsync();
        }
        else if (!wasDrag)
        {
            if (_dictation.IsRecording)
                _ = _dictation.StopRecordingAsync();
            else
                _ = _dictation.StartRecordingAsync();
        }
    }

    private void OnButtonCancelled(object? sender, EventArgs e)
    {
        _buttonWasDragged = true;
        // Hold mode: recording continues; ButtonUp fires after DragMove completes
    }

    public void Dispose()
    {
        _settings.SettingsChanged -= OnSettingsChanged;
        Application.Current.Dispatcher.Invoke(CloseWindow);
    }
}
