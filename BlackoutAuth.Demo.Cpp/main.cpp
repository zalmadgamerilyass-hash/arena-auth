#include <iostream>
#include "../BlackoutAuth.SDK.Cpp/include/BlackoutAuth.hpp"

void SetConsoleColor(WORD color)
{
    SetConsoleTextAttribute(GetStdHandle(STD_OUTPUT_HANDLE), color);
}

int main()
{
    SetConsoleTitleA("BLACKOUT AUTH - C++ Integration Demo");

    SetConsoleColor(FOREGROUND_RED | FOREGROUND_BLUE | FOREGROUND_INTENSITY); // Magenta
    std::cout << "==================================================================" << std::endl;
    std::cout << "             ⚡ BLACKOUT AUTH - C++ CLIENT DEMO ⚡               " << std::endl;
    std::cout << "==================================================================" << std::endl;
    SetConsoleColor(FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE);

    // 1. Initialize Blackout Auth Client
    // enableAntiDebug=true will check for debuggers/cheat engines on Init()
    Blackout::Auth app(
        "BlackoutApp",
        "OWNER-001",
        "blackout_secret_9999a8b7c6d5e4f321",
        "1.0",
        "https://blackout-auth-production.up.railway.app", // <-- CHANGE THIS to your Railway URL
        true // enableAntiDebug: enables anti-crack detection
    );

    SetConsoleColor(FOREGROUND_GREEN | FOREGROUND_BLUE | FOREGROUND_INTENSITY); // Cyan
    std::cout << "[*] Initializing secure handshake with server... ";
    SetConsoleColor(FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE);

    if (!app.Init())
    {
        SetConsoleColor(FOREGROUND_RED | FOREGROUND_INTENSITY);
        std::cout << "\n[-] Handshake Failed: " << app.GetLastError() << std::endl;
        std::cout << "[!] Make sure BlackoutAuth.Server is running on http://localhost:5000" << std::endl;
        SetConsoleColor(FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE);
        std::cout << "\nPress Enter to exit...";
        std::cin.get();
        return 1;
    }

    SetConsoleColor(FOREGROUND_GREEN | FOREGROUND_INTENSITY);
    std::cout << "SUCCESS!" << std::endl;
    SetConsoleColor(FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE);
    std::cout << "[+] Client HWID: " << app.Hwid << std::endl;

    while (true)
    {
        SetConsoleColor(FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_INTENSITY); // Yellow
        std::cout << "\n------------------------------------------------------------------" << std::endl;
        std::cout << "  SELECT AUTHENTICATION METHOD:" << std::endl;
        std::cout << "  [1] License Key Only (Instant KeyAuth-Style Activation)" << std::endl;
        std::cout << "  [2] Login (Username & Password)" << std::endl;
        std::cout << "  [3] Register (Username, Password & Key)" << std::endl;
        std::cout << "  [4] Exit" << std::endl;
        std::cout << "------------------------------------------------------------------" << std::endl;
        SetConsoleColor(FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE);
        std::cout << "Choice: ";

        int choice = 0;
        if (!(std::cin >> choice))
        {
            std::cin.clear();
            std::cin.ignore(1000, '\n');
            continue;
        }

        if (choice == 1)
        {
            std::cout << "\nEnter License Key (e.g. BLACKOUT-DEMO-30DAYS-1337): ";
            std::string key;
            std::cin >> key;

            SetConsoleColor(FOREGROUND_GREEN | FOREGROUND_BLUE | FOREGROUND_INTENSITY);
            std::cout << "[*] Verifying license key... ";
            SetConsoleColor(FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE);

            if (app.LicenseOnly(key))
            {
                SetConsoleColor(FOREGROUND_GREEN | FOREGROUND_INTENSITY);
                std::cout << "SUCCESS!" << std::endl;
                SetConsoleColor(FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE);
                break;
            }
            else
            {
                SetConsoleColor(FOREGROUND_RED | FOREGROUND_INTENSITY);
                std::cout << "FAILED: " << app.GetLastError() << std::endl;
                SetConsoleColor(FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE);
            }
        }
        else if (choice == 2)
        {
            std::cout << "\nEnter Username: ";
            std::string user;
            std::cin >> user;
            std::cout << "Enter Password: ";
            std::string pass;
            std::cin >> pass;

            SetConsoleColor(FOREGROUND_GREEN | FOREGROUND_BLUE | FOREGROUND_INTENSITY);
            std::cout << "[*] Authenticating... ";
            SetConsoleColor(FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE);

            if (app.Login(user, pass))
            {
                SetConsoleColor(FOREGROUND_GREEN | FOREGROUND_INTENSITY);
                std::cout << "SUCCESS!" << std::endl;
                SetConsoleColor(FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE);
                break;
            }
            else
            {
                SetConsoleColor(FOREGROUND_RED | FOREGROUND_INTENSITY);
                std::cout << "FAILED: " << app.GetLastError() << std::endl;
                SetConsoleColor(FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE);
            }
        }
        else if (choice == 3)
        {
            std::cout << "\nChoose Username: ";
            std::string user;
            std::cin >> user;
            std::cout << "Choose Password: ";
            std::string pass;
            std::cin >> pass;
            std::cout << "Enter License Key: ";
            std::string key;
            std::cin >> key;

            SetConsoleColor(FOREGROUND_GREEN | FOREGROUND_BLUE | FOREGROUND_INTENSITY);
            std::cout << "[*] Registering account... ";
            SetConsoleColor(FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE);

            if (app.Register(user, pass, key))
            {
                SetConsoleColor(FOREGROUND_GREEN | FOREGROUND_INTENSITY);
                std::cout << "SUCCESS!" << std::endl;
                SetConsoleColor(FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE);
                break;
            }
            else
            {
                SetConsoleColor(FOREGROUND_RED | FOREGROUND_INTENSITY);
                std::cout << "FAILED: " << app.GetLastError() << std::endl;
                SetConsoleColor(FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE);
            }
        }
        else if (choice == 4)
        {
            return 0;
        }
    }

    // Access granted
    SetConsoleColor(FOREGROUND_GREEN | FOREGROUND_INTENSITY);
    std::cout << "\n==================================================================" << std::endl;
    std::cout << "               👑 ACCESS GRANTED TO BLACKOUT CORE 👑              " << std::endl;
    std::cout << "==================================================================" << std::endl;
    SetConsoleColor(FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE);

    std::cout << "  User:       " << app.User.Username << std::endl;
    std::cout << "  Expires:    " << app.User.Expiry << std::endl;
    std::cout << "  Rank Level: " << app.User.Level << std::endl;
    std::cout << "  HWID:       " << app.User.Hwid << std::endl;

    // Retrieve encrypted cloud variable
    SetConsoleColor(FOREGROUND_GREEN | FOREGROUND_BLUE | FOREGROUND_INTENSITY);
    std::cout << "\n[*] Fetching decrypted cloud variable 'client_greeting'... ";
    SetConsoleColor(FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE);

    std::string greeting = app.GetVar("client_greeting");
    if (!greeting.empty())
    {
        SetConsoleColor(FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_INTENSITY);
        std::cout << "\n    >> \"" << greeting << "\"" << std::endl;
        SetConsoleColor(FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE);
    }
    else
    {
        std::cout << "Failed to fetch variable." << std::endl;
    }

    // Heartbeat check
    bool alive = app.CheckSession();
    std::cout << "\n[+] Session Keep-Alive Status: " << (alive ? "ACTIVE & VALID" : "EXPIRED") << std::endl;

    // Log client event
    app.Log("C++ Client entered main loop.", "INFO");
    std::cout << "[+] Log dispatch sent to server." << std::endl;

    std::cout << "\nPress Enter to exit...";
    std::cin.ignore(1000, '\n');
    std::cin.get();

    return 0;
}
