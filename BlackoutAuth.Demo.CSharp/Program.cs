using System;
using BlackoutAuth.SDK.CSharp;

namespace BlackoutAuth.Demo.CSharp
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.Title = "BLACKOUT AUTH — C# Integration Demo";
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("==================================================================");
            Console.WriteLine("             ⚡ BLACKOUT AUTH - C# CLIENT DEMO ⚡                ");
            Console.WriteLine("==================================================================");
            Console.ResetColor();

            // 1. Initialize Blackout Auth Client
            // Connects to local server (or your remote VPS domain)
            var auth = new BlackoutApp(
                name: "BlackoutApp",
                ownerId: "OWNER-001",
                secret: "blackout_secret_9999a8b7c6d5e4f321",
                version: "1.0",
                apiUrl: "http://localhost:5000",
                enableAntiDebug: false // set to true in production
            );

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("[*] Initializing secure session with server... ");
            Console.ResetColor();

            if (!auth.Init())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[-] Initialization Failed: " + auth.LastError);
                Console.WriteLine("[!] Make sure BlackoutAuth.Server is running on http://localhost:5000");
                Console.ResetColor();
                Console.WriteLine("\nPress Enter to exit...");
                Console.ReadLine();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("SUCCESS!");
            Console.ResetColor();
            Console.WriteLine($"[+] HWID: {auth.HwidString}");

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n------------------------------------------------------------------");
                Console.WriteLine("  SELECT AUTHENTICATION MODE:");
                Console.WriteLine("  [1] License Key Only (Quick KeyAuth-Style Activation)");
                Console.WriteLine("  [2] Login (Username & Password)");
                Console.WriteLine("  [3] Register (Username, Password & License Key)");
                Console.WriteLine("  [4] Exit");
                Console.WriteLine("------------------------------------------------------------------");
                Console.ResetColor();
                Console.Write("Choice: ");

                string? choice = Console.ReadLine()?.Trim();

                if (choice == "1")
                {
                    Console.Write("\nEnter License Key (e.g. BLACKOUT-DEMO-30DAYS-1337): ");
                    string? key = Console.ReadLine()?.Trim();

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write("[*] Verifying license... ");
                    Console.ResetColor();

                    if (auth.LicenseOnly(key ?? ""))
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("SUCCESS!");
                        Console.ResetColor();
                        OnAuthSuccess(auth);
                        break;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("FAILED: " + auth.LastError);
                        Console.ResetColor();
                    }
                }
                else if (choice == "2")
                {
                    Console.Write("\nEnter Username: ");
                    string? user = Console.ReadLine()?.Trim();
                    Console.Write("Enter Password: ");
                    string? pass = Console.ReadLine()?.Trim();

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write("[*] Authenticating... ");
                    Console.ResetColor();

                    if (auth.Login(user ?? "", pass ?? ""))
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("SUCCESS!");
                        Console.ResetColor();
                        OnAuthSuccess(auth);
                        break;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("FAILED: " + auth.LastError);
                        Console.ResetColor();
                    }
                }
                else if (choice == "3")
                {
                    Console.Write("\nChoose Username: ");
                    string? user = Console.ReadLine()?.Trim();
                    Console.Write("Choose Password: ");
                    string? pass = Console.ReadLine()?.Trim();
                    Console.Write("Enter License Key: ");
                    string? key = Console.ReadLine()?.Trim();

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write("[*] Registering account... ");
                    Console.ResetColor();

                    if (auth.Register(user ?? "", pass ?? "", key ?? ""))
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("SUCCESS!");
                        Console.ResetColor();
                        OnAuthSuccess(auth);
                        break;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("FAILED: " + auth.LastError);
                        Console.ResetColor();
                    }
                }
                else if (choice == "4")
                {
                    return;
                }
            }

            Console.WriteLine("\nPress Enter to exit...");
            Console.ReadLine();
        }

        private static void OnAuthSuccess(BlackoutApp auth)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n==================================================================");
            Console.WriteLine("               👑 ACCESS GRANTED TO BLACKOUT CORE 👑              ");
            Console.WriteLine("==================================================================");
            Console.ResetColor();

            Console.WriteLine($"  User:       {auth.User?.Username}");
            Console.WriteLine($"  Expires:    {auth.User?.Expiry}");
            Console.WriteLine($"  Rank Level: {auth.User?.Level}");
            Console.WriteLine($"  HWID:       {auth.User?.Hwid}");

            // Fetch encrypted cloud variable
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("\n[*] Fetching decrypted cloud variable 'client_greeting'... ");
            Console.ResetColor();

            string? greeting = auth.GetVar("client_greeting");
            if (greeting != null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n    >> \"{greeting}\"");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine("Not found or level insufficient.");
            }

            // Test session heartbeat check
            bool alive = auth.Check();
            Console.WriteLine($"\n[+] Session Keep-Alive Status: {(alive ? "ALIVE & VALID" : "EXPIRED")}");

            // Log client message to admin dashboard
            auth.Log("User successfully entered client dashboard.", "INFO");
            Console.WriteLine("[+] Security audit log recorded on server.");
        }
    }
}
