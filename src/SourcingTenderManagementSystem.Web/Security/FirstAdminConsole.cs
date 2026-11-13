using System.Text;

namespace SourcingTenderManagementSystem.Web.Security;

public interface IFirstAdminConsole
{
    bool IsInteractive { get; }
    void WriteLine(string message);
    string ReadLine(string prompt);
    string ReadPassword(string prompt);
}

public sealed class FirstAdminConsole : IFirstAdminConsole
{
    public bool IsInteractive => !Console.IsInputRedirected && !Console.IsOutputRedirected;
    public void WriteLine(string message) => Console.WriteLine(message);

    public string ReadLine(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine() ?? throw new OperationCanceledException();
    }

    public string ReadPassword(string prompt)
    {
        if (!IsInteractive) throw new InvalidOperationException("An interactive console is required.");
        Console.Write(prompt);
        var input = new StringBuilder();
        try
        {
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    return input.ToString();
                }
                if (key.Key == ConsoleKey.Escape) throw new OperationCanceledException();
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (input.Length > 0) input.Length--;
                }
                else if (!char.IsControl(key.KeyChar)) input.Append(key.KeyChar);
            }
        }
        finally { input.Clear(); }
    }
}
