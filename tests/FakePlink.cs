using System;
using System.IO;
using System.Linq;

static class FakePlink
{
    static int Main(string[] args)
    {
        if (args.Contains("transport-retry"))
        {
            string marker = Path.Combine(Environment.CurrentDirectory, "fake-plink-transport-retry.marker");
            if (!File.Exists(marker))
            {
                File.WriteAllText(marker, "first attempt");
                Console.Error.WriteLine("FATAL ERROR: Network error: Software caused connection abort");
                return 1;
            }
            File.Delete(marker);
            Console.WriteLine("TRANSIENT_RETRY=OK");
            return 0;
        }

        bool batch = args.Contains("-batch");
        bool hasHostKey = args.Contains("-hostkey");
        int userIndex = Array.IndexOf(args, "-l");

        // NetStuck must discover an uncached key without sending a username or password.
        if (batch && userIndex < 0)
        {
            if (args.Contains("-pw"))
            {
                Console.Error.WriteLine("Preflight exposed a password in argv.");
                return 10;
            }
            Console.Error.WriteLine("The host key is not cached for this server.");
            Console.Error.WriteLine("SHA256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=");
            return 1;
        }

        string username = userIndex >= 0 && userIndex + 1 < args.Length ? args[userIndex + 1] : "";
        if (username != "company\\myname")
        {
            Console.Error.WriteLine("Unexpected username; literal backslashes=" + username.Count(c => c == '\\'));
            return 9;
        }
        if (!hasHostKey || batch || args.Contains("-pw"))
        {
            Console.Error.WriteLine("Interactive SSH arguments were unsafe or incomplete.");
            return 8;
        }

        string authCountFile = Path.Combine(Environment.CurrentDirectory, "fake-plink-auth-count.txt");
        int authCount = 0;
        if (File.Exists(authCountFile)) Int32.TryParse(File.ReadAllText(authCountFile), out authCount);
        File.WriteAllText(authCountFile, (authCount + 1).ToString());

        Console.Error.Write("Password: ");
        Console.Error.Flush();
        string password = Console.ReadLine();
        if (password != "testpass")
        {
            Console.Error.WriteLine("Access denied");
            return 6;
        }

        Console.Write("edge-sw> ");
        Console.Out.Flush();
        while (true)
        {
            string command = Console.ReadLine();
            if (command == null || command.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;
            Console.WriteLine(command);
            if (command.Equals("show version", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("MOCK SSH CONFIG OUTPUT");
                Console.WriteLine("USER_BACKSLASHES=" + username.Count(c => c == '\\'));
                Console.WriteLine("KEYBOARD_INTERACTIVE_FALLBACK=OK");
                Console.WriteLine("PROMPT_AWARE_COMMAND=OK");
            }
            if (command.Equals("show large", StringComparison.OrdinalIgnoreCase))
            {
                string payload = new string('X', 96);
                for (int line = 0; line < 25000; line++) Console.WriteLine("CONFIG-" + line.ToString("00000") + " " + payload);
            }
            Console.Write("edge-sw> ");
            Console.Out.Flush();
        }
        return 0;
    }
}
