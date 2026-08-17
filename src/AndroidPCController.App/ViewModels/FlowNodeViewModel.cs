using AndroidPCController.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AndroidPCController.App.ViewModels;

public partial class FlowNodeViewModel : ObservableObject
{
    public const double NodeWidth = 240;
    public const double HeaderHeight = 36;

    public AutomationAction Action { get; }

    public string Title { get; }

    public string Icon { get; }

    public string AccentColor { get; }

    public string Description { get; }

    public bool HasXY => Action is AutomationAction.Tap or AutomationAction.LongPress or AutomationAction.Swipe;

    public bool HasEnd => Action == AutomationAction.Swipe;

    public bool HasDuration => Action is AutomationAction.LongPress or AutomationAction.Swipe;

    public bool HasDelay => Action is AutomationAction.Wait or AutomationAction.LaunchApp;

    public bool HasKey => Action == AutomationAction.PressKey;

    public bool HasText => Action is AutomationAction.InputText or AutomationAction.LaunchApp;

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private int _xCoord;

    [ObservableProperty]
    private int _yCoord;

    [ObservableProperty]
    private int _endX;

    [ObservableProperty]
    private int _endY;

    [ObservableProperty]
    private int _durationMs = 300;

    [ObservableProperty]
    private int _delayMs;

    [ObservableProperty]
    private int _keyCode = 4;

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private bool _isCurrent;

    public FlowNodeViewModel(AutomationAction action, double x, double y)
    {
        Action = action;
        X = x;
        Y = y;

        (Title, Icon, AccentColor, Description) = action switch
        {
            AutomationAction.LaunchApp => ("Launch App", "\uE7B4", "#00D2FF", "Open an app by package name"),
            AutomationAction.Wait => ("Wait", "\uE823", "#F59E0B", "Pause for a delay"),
            AutomationAction.Tap => ("Tap", "\uE7C6", "#22C55E", "Tap at coordinates"),
            AutomationAction.LongPress => ("Long Press", "\uE7C6", "#22C55E", "Press and hold"),
            AutomationAction.Swipe => ("Swipe", "\uE70C", "#4D7CFF", "Drag between points"),
            AutomationAction.PressKey => ("Press Key", "\uE8AB", "#A855F7", "Send a key code"),
            AutomationAction.InputText => ("Input Text", "\uE8F4", "#00D2FF", "Type text"),
            AutomationAction.TakeScreenshot => ("Screenshot", "\uE722", "#F59E0B", "Capture the screen"),
            AutomationAction.StopRecording => ("Stop Recording", "\uE71A", "#EF4444", "End screen recording"),
            AutomationAction.Back => ("Back", "\uE711", "#9E9E9E", "Navigate back"),
            AutomationAction.Home => ("Home", "\uE80F", "#9E9E9E", "Go to home screen"),
            AutomationAction.Recent => ("Recents", "\uE8B0", "#9E9E9E", "Open recent apps"),
            AutomationAction.Sleep => ("Sleep", "\uE7E8", "#9E9E9E", "Turn screen off"),
            _ => ("Step", "\uE823", "#9E9E9E", "Automation step")
        };
    }

    public AutomationStep ToStep() => new()
    {
        Action = Action,
        X = XCoord,
        Y = YCoord,
        EndX = EndX,
        EndY = EndY,
        KeyCode = KeyCode,
        Text = Text,
        DelayMs = DelayMs,
        DurationMs = DurationMs,
        Description = Title
    };
}