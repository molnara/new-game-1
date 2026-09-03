using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

namespace NewGame1.Tests;

public class SmokeTest : TestClass
{
    public SmokeTest(Node testScene) : base(testScene) { }

    [Test]
    public void PassesTrivially()
    {
        TestScene.ShouldNotBeNull();
    }
}
