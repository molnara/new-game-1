using Chickensoft.GoDotTest;
using Godot;
using NewGame1.Autoloads;
using Shouldly;

namespace NewGame1.Tests;

public class ConsoleInputTest : TestClass
{
    private readonly DevConsole _console;

    public ConsoleInputTest(Node testScene) : base(testScene)
    {
        _console = testScene.GetNode<DevConsole>("/root/DevConsole");
    }

    [Test]
    public async Task ToggleOpensAndClosesTheConsole()
    {
        _console.IsOpen.ShouldBeFalse();

        PressToggle();
        await WaitFrame();
        await WaitFrame();
        _console.IsOpen.ShouldBeTrue();

        PressToggle();
        await WaitFrame();
        _console.IsOpen.ShouldBeFalse();
    }

    [Test]
    public async Task ToggleKeystrokeDoesNotLeakIntoTheInputField()
    {
        PressToggle();
        await WaitFrame();
        await WaitFrame();

        _console.IsOpen.ShouldBeTrue();
        _console.InputText.ShouldBeEmpty();

        PressToggle();
        await WaitFrame();
        _console.IsOpen.ShouldBeFalse();
    }

    [Test]
    public async Task ConsoleIsVisibleWithinASingleDisplayedFrame()
    {
        _console.IsOpen.ShouldBeFalse();

        PressToggle();
        await WaitFrame();
        await WaitFrame();
        _console.IsOpen.ShouldBeTrue();

        PressToggle();
        await WaitFrame();
        _console.IsOpen.ShouldBeFalse();
    }

    // Two ticks, empirically: a synthetic Input.ParseInputEvent() plus GoDotTest's own async
    // scheduling take one process_frame signal to settle before the dispatched _UnhandledKeyInput
    // is even queued. A real key press does not carry this harness-only latency.
    private async Task WaitFrame() => await TestScene.ToSignal(TestScene.GetTree(), SceneTree.SignalName.ProcessFrame);

    private static void PressToggle() => Input.ParseInputEvent(new InputEventKey
    {
        Keycode = Key.Quoteleft,
        PhysicalKeycode = Key.Quoteleft,
        Pressed = true,
    });
}
