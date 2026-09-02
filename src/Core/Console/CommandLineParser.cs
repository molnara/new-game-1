using System.Text;

namespace NewGame1.Core.Console;

/// <summary>
/// Splits one console input line into a command name and its positional arguments. Whitespace
/// separates tokens; double quotes hold a token containing spaces together. An unterminated quote
/// is a parse failure — nothing runs.
/// </summary>
public static class CommandLineParser
{
    public static bool TryParse(string line, out CommandArgs? args)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var hasToken = false;

        foreach (var c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                hasToken = true;
                continue;
            }

            if (!inQuotes && char.IsWhiteSpace(c))
            {
                if (hasToken)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                    hasToken = false;
                }

                continue;
            }

            current.Append(c);
            hasToken = true;
        }

        if (inQuotes)
        {
            args = null;
            return false;
        }

        if (hasToken)
        {
            tokens.Add(current.ToString());
        }

        if (tokens.Count == 0)
        {
            args = null;
            return false;
        }

        args = new CommandArgs(tokens[0], tokens.Skip(1).ToList());
        return true;
    }
}
