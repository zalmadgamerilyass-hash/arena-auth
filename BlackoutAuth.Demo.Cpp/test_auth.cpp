#include <iostream>
#include "BlackoutAuth.hpp"

int main() {
    std::cout << "=== BLACKOUT AUTH TEST ===" << std::endl;
    std::cout << std::endl;

    // Initialize the auth client
    Blackout::Auth auth(
        "BlackoutApp",
        "OWNER-001",
        "blackout_secret_9999a8b7c6d5e4f321",
        "1.0",
        "http://localhost:5000"
    );

    std::cout << "[*] Initializing connection to server..." << std::endl;
    if (!auth.Init()) {
        std::cout << "[!] Init failed: " << auth.GetLastError() << std::endl;
        std::cout << "[!] Make sure the server is running on http://localhost:5000" << std::endl;
        std::cout << std::endl;
        std::cout << "Press Enter to exit...";
        std::cin.get();
        return 1;
    }
    std::cout << "[+] Init successful!" << std::endl;
    std::cout << std::endl;

    // Try to login with demo credentials
    std::string username = "demo";
    std::string password = "demo1234";

    std::cout << "[*] Attempting login with:" << std::endl;
    std::cout << "    Username: " << username << std::endl;
    std::cout << "    Password: " << password << std::endl;
    std::cout << std::endl;

    if (auth.Login(username, password)) {
        std::cout << "[+] LOGIN SUCCESSFUL!" << std::endl;
        std::cout << std::endl;
        std::cout << "User Info:" << std::endl;
        std::cout << "  Username: " << auth.User.Username << std::endl;
        std::cout << "  Expiry:   " << auth.User.Expiry << std::endl;
        std::cout << "  Level:    " << auth.User.Level << std::endl;
        std::cout << "  HWID:     " << auth.User.Hwid << std::endl;
        std::cout << std::endl;
        std::cout << "[+] The auth is working correctly!" << std::endl;
    } else {
        std::cout << "[!] LOGIN FAILED!" << std::endl;
        std::cout << "    Error: " << auth.GetLastError() << std::endl;
        std::cout << std::endl;
        std::cout << "Possible issues:" << std::endl;
        std::cout << "  - Server not running on http://localhost:5000" << std::endl;
        std::cout << "  - User 'demo' does not exist or password is wrong" << std::endl;
        std::cout << "  - Check server logs for details" << std::endl;
    }

    std::cout << std::endl;
    std::cout << "Press Enter to exit...";
    std::cin.get();
    return 0;
}
