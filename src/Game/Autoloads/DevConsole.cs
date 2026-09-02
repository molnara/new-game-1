using Godot;
using Microsoft.Extensions.Logging;
using NewGame1.Core.Console;
using NewGame1.Core.Diagnostics;
using NewGame1.Infrastructure;

namespace NewGame1.Autoloads;

public partial class DevConsole : CanvasLayer
{
    private const int HistoryCapacity = 1000;

    public CommandRegistry Registry { get; } = new();

    public bool IsOpen => _panel.Visible;

    public string InputText => _input.Text;

    private readonly BoundedLog _history = new(HistoryCapacity);
    private readonly List<string> _submitted = [];
    private ColorRect _panel = null!;
    private RichTextLabel _output = null!;
    private LineEdit _input = null!;
    private ILogger<DevConsole> _logger = null!;
    private int _historyCursor;
    private bool _openAllowed;

    public override void _Ready()
    {
        Logging.Initialize();
        _logger = Logging.For<DevConsole>();
        ProcessMode = ProcessModeEnum.Always;
        Layer = 100;

        _openAllowed = DetermineOpenAllowed(_logger);

        HelpCommand.Register(Registry);

        BuildUi();
        SetOpen(false);
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event.IsActionPressed("console_toggle"))
        {
            GetViewport().SetInputAsHandled();

            if (_panel.Visible || _openAllowed)
            {
                SetOpen(!_panel.Visible);
            }

            return;
        }

        if (!_panel.Visible || @event is not InputEventKey { Pressed: true } key)
        {
            return;
        }

        if (key.Keycode == Key.Up)
        {
            RecallHistory(-1);
            GetViewport().SetInputAsHandled();
        }
        else if (key.Keycode == Key.Down)
        {
            RecallHistory(1);
            GetViewport().SetInputAsHandled();
        }
    }

    private static bool DetermineOpenAllowed(ILogger<DevConsole> logger)
    {
        var isExportedRelease = OS.HasFeature("template_release");
        var isEditorRun = OS.HasFeature("editor");
        var devConsoleFlag = OS.GetCmdlineUserArgs().Contains("--dev-console");
        var allowed = !isExportedRelease || devConsoleFlag;

        logger.LogInformation(
            "Dev console gating: exportedRelease={ExportedRelease} editorRun={EditorRun} devConsoleFlag={DevConsoleFlag} allowed={Allowed}",
            isExportedRelease, isEditorRun, devConsoleFlag, allowed);

        return allowed;
    }

    private void BuildUi()
    {
        _panel = new ColorRect { Color = new Color(0f, 0f, 0f, 0.85f) };
        AddChild(_panel);
        _panel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        _panel.OffsetBottom = 320;

        var layout = new VBoxContainer();
        _panel.AddChild(layout);
        layout.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layout.OffsetLeft = 8;
        layout.OffsetTop = 8;
        layout.OffsetRight = -8;
        layout.OffsetBottom = -8;

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        layout.AddChild(scroll);

        _output = new RichTextLabel
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            ScrollFollowing = true,
            BbcodeEnabled = false,
        };
        scroll.AddChild(_output);

        _input = new LineEdit();
        _input.TextSubmitted += OnSubmitted;
        layout.AddChild(_input);
    }

    private void SetOpen(bool open)
    {
        _panel.Visible = open;

        if (open)
        {
            _input.GrabFocus();
        }
        else
        {
            _input.ReleaseFocus();
        }
    }

    private void OnSubmitted(string line)
    {
        _input.Clear();

        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        _submitted.Add(line);
        _historyCursor = _submitted.Count;

        AppendLine("> " + line);
        var result = Registry.Execute(line);

        if (result.Succeeded)
        {
            _logger.LogInformation("Command {Line} succeeded: {Message}", line, result.Message);
            AppendLine(result.Message);
        }
        else
        {
            _logger.LogWarning("Command {Line} failed: {Reason}", line, result.FailureReason);
            AppendLine($"[error] {result.FailureReason}");
        }
    }

    private void RecallHistory(int direction)
    {
        if (_submitted.Count == 0)
        {
            return;
        }

        _historyCursor = Math.Clamp(_historyCursor + direction, 0, _submitted.Count - 1);
        _input.Text = _submitted[_historyCursor];
        _input.CaretColumn = _input.Text.Length;
    }

    private void AppendLine(string line)
    {
        _history.Add(line);
        _output.Text = string.Join('\n', _history.Entries);
    }
}
