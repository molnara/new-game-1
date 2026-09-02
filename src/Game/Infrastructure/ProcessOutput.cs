using Godot;

namespace NewGame1.Infrastructure;

// Plain, unformatted lines on the process's own stdout/stderr, for command-line entry points whose
// caller is a shell script rather than a reader of the log. `scripts/screenshot.sh` reads the
// captured path from stdout and surfaces the failure reason from stderr (contracts/cli-scripts.md),
// so those lines must stay bare — the log pipeline is for the record and timestamps every line.
//
// GD.Print/GD.PrintErr rather than System.Console keeps the current behaviour: the same lines also
// reach Godot's own log and the editor output panel. Together with GodotSink this is one of the two
// files permitted to call them (constitution III).
public static class ProcessOutput
{
    public static void WriteLine(string line) => GD.Print(line);

    public static void WriteErrorLine(string line) => GD.PrintErr(line);
}
