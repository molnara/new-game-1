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
        // Exactly one WaitFrame — that single process_frame signal is entirely harness-only
        // latency: a synthetic Input.ParseInputEvent() plus GoDotTest's own async scheduling
        // take one tick to settle before the dispatched _Input is even queued (a real key press
        // does not carry this). Asserting True right after this one wait, with no second
        // WaitFrame, is what actually measures SC-007 — the console must be visible in the same
        // displayed frame the toggle is processed, not one frame later.
        await WaitFrame();
        _console.IsOpen.ShouldBeTrue();

        PressToggle();
        await WaitFrame();
        _console.IsOpen.ShouldBeFalse();
    }

    private async Task WaitFrame() => await TestScene.ToSignal(TestScene.GetTree(), SceneTree.SignalName.ProcessFrame);

    // Unicode = 96 ('`'): a real keyboard sends this alongside the keycode. Without it a
    // focused LineEdit has nothing to insert as text, so the leak FR-011 guards against never
    // gets exercised.
    private static void PressToggle() => Input.ParseInputEvent(new InputEventKey
    {
        Keycode = Key.Quoteleft,
        PhysicalKeycode = Key.Quoteleft,
        Unicode = 96,
        Pressed = true,
    });
}
