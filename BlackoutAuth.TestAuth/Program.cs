using System;
using BlackoutAuth.SDK.CSharp;

namespace BlackoutAuth.TestAuth
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "BLACKOUT AUTH - TEST";
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("========================================");
            Console.WriteLine("     BLACKOUT AUTH - TEST EXE");
            Console.WriteLine("========================================\n");
            Console.ResetColor();

            var auth = new BlackoutApp(
                name: "BlackoutApp",
                ownerId: "OWNER-001",
                secret: "blackout_secret_9999a8b7c6d5e4f321",
                version: "1.0",
                apiUrl: "http://localhost:5000"
            );

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("[*] Initializing connection to server... ");
            Console.ResetColor();

            if (!auth.Init())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAILED!");
                Console.ResetColor();
                Console.WriteLine($"    Error: {auth.LastError}");
                Console.WriteLine("\n[!] Make sure the server is running on http://localhost:5000");
                Console.WriteLine("    Run: BlackoutAuth.Server.exe or start-server.bat\n");
                Console.WriteLine("Press Enter to exit...");
                Console.ReadLine();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("SUCCESS!");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\nEnter credentials (or press Enter for demo/demo1234):");
            Console.ResetColor();

            Console.Write("Username: ");
            string username = Console.ReadLine() ?? "";
            if (string.IsNullOrEmpty(username)) username = "demo";

            Console.Write("Password: ");
            string password = Console.ReadLine() ?? "";
            if (string.IsNullOrEmpty(password)) password = "demo1234";

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"\n[*] Authenticating as '{username}'... ");
            Console.ResetColor();

            if (auth.Login(username, password))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("SUCCESS!\n");
                Console.ResetColor();

                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("┌─────────────────────────────────────┐");
                Console.WriteLine("│         USER INFORMATION            │");
                Console.WriteLine("├─────────────────────────────────────┤");
                Console.WriteLine($"│  Username: {auth.User?.Username,-20} │");
                Console.WriteLine($"│  Expiry:   {auth.User?.Expiry,-20} │");
                Console.WriteLine($"│  Level:    {auth.User?.Level,-20} │");
                Console.WriteLine($"│  HWID:     {(auth.User?.Hwid ?? "N/A"),-20} │");
                Console.WriteLine("└─────────────────────────────────────┘\n");
                Console.ResetColor();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✅ The authentication is WORKING!");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAILED!\n");
                Console.ResetColor();

                Console.WriteLine($"Error: {auth.LastError}\n");
                Console.WriteLine("Possible reasons:");
                Console.WriteLine("  - Wrong username/password");
                Console.WriteLine("  - User not found");
                Console.WriteLine("  - Server not running\n");
            }

            Console.WriteLine("Press Enter to exit...");
            Console.ReadLine();
        }
    }
}
