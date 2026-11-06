# ⚡ ARENA AUTH — Next-Gen Licensing & Identity Engine

**ARENA AUTH** is a secure, high-performance licensing and authentication system inspired by KeyAuth, built from scratch with an ultra-sleek, stealth-dark cyber dashboard and seamless integration libraries for **C#** and **C++** applications.

---

## 🌟 Key Features

- 🛡️ **Zero-Dependency C++ Client SDK**: Header-only (`BlackoutAuth.hpp`) using native Windows APIs (`WinHTTP` and `BCrypt`). No `libcurl`, `OpenSSL`, or `vcpkg` required!
- ⚡ **Lightweight C# Client SDK**: Drop-in .NET class library (`ArenaApp`) with native hardware ID hashing and anti-debugging safeguards.
- 🎨 **Stealth Dark Admin Dashboard**: Obsidian-dark aesthetic with neon violet/cyan accents, live authentication traffic charts, real-time security alerts, and glassmorphic panels.
- 🔑 **Flexible License Management**:
  - Bulk key generation (custom prefix, durations: 1d, 7d, 30d, 90d, 365d, or Lifetime, rank levels, notes).
  - One-click copy, bulk export to `.txt`, instant status toggles (Unused, Paused, Banned).
- 👤 **Hardware ID (HWID) Locking & Resets**: Automatically locks license keys and accounts to the user's motherboard, disk, and CPU fingerprint. One-click HWID reset via web dashboard.
- 🔒 **Dynamic Session Encryption**: Real-time session token generation with dynamic AES-256-CBC payload encryption and SHA-256 tampering prevention.
- ☁️ **Cloud Variables & Protected Files**: Store protected offsets, sensitive API tokens, and stream binary payloads (DLLs, loaders, configs) securely to authorized users.
- 📡 **Discord Webhook Logging**: Real-time embed notifications for registrations, logins, bans, and HWID mismatch alerts.
- 💾 **Self-Contained SQLite Database**: Zero database server setup needed. The database (`blackout_auth.db`) initializes and seeds itself on first run.

---

## 🚀 Quick Start Guide

### 1. Launch the Server

Double-click `start-server.bat` or run in terminal:

```bash
cd "BlackoutAuth.Server"
dotnet run
```

The server will automatically start on:
- **Admin Dashboard**: `http://localhost:5000/index.html`
- **Client API Endpoint**: `http://localhost:5000/api/v1/auth`

### 2. Default Admin Credentials

| Field | Default Value |
|---|---|
| **Portal URL** | [http://localhost:5000](http://localhost:5000) |
| **Username** | `admin` |
| **Password** | `arena1337` |
| **Sample App** | `ArenaApp` |
| **Owner ID** | `OWNER-001` |
| **App Secret** | `arena_secret_9999a8b7c6d5e4f321` |

*(You can change the admin password anytime in the **Settings** tab of the dashboard).*

---

## 💻 C# Integration (Easy Drop-in)

Add a reference to `BlackoutAuth.SDK.CSharp.dll` or include the project:

```csharp
using BlackoutAuth.SDK.CSharp;

// 1. Initialize client
var auth = new ArenaApp(
    name: "ArenaApp",
    ownerId: "OWNER-001",
    secret: "arena_secret_9999a8b7c6d5e4f321",
    version: "1.0",
    apiUrl: "http://localhost:5000" // Replace with your domain in production
);

// 2. Initialize connection
if (!auth.Init())
{
    Console.WriteLine("Init Error: " + auth.LastError);
    return;
}

// 3. Option A: License Key Only (Instant KeyAuth style)
if (auth.LicenseOnly("ARENA-DEMO-30DAYS-1337"))
{
    Console.WriteLine($"Welcome! Subscription expires: {auth.User.Expiry}");
}

// 4. Option B: Username & Password Login
// if (auth.Login("username", "password")) { ... }

// 5. Option C: Register new account
// if (auth.Register("username", "password", "LICENSE-KEY")) { ... }

// 6. Retrieve Decrypted Cloud Variable
string? secretVar = auth.GetVar("client_greeting");
Console.WriteLine("Cloud secret: " + secretVar);
```

---

## ⚡ C++ Integration (Zero External Dependencies!)

Just `#include "BlackoutAuth.hpp"` in your Visual Studio project. It automatically links `winhttp.lib`, `bcrypt.lib`, and `crypt32.lib`:

```cpp
#include <iostream>
#include "BlackoutAuth.hpp"

int main()
{
    // 1. Initialize client
    Blackout::Auth auth(
        "ArenaApp",
        "OWNER-001",
        "arena_secret_9999a8b7c6d5e4f321",
        "1.0",
        "http://localhost:5000"
    );

    // 2. Handshake with server
    if (!auth.Init())
    {
        std::cout << "Handshake failed: " << auth.GetLastError() << std::endl;
        return 1;
    }

    // 3. Authenticate with license key
    if (auth.LicenseOnly("ARENA-DEMO-30DAYS-1337"))
    {
        std::cout << "Authenticated! Expiry: " << auth.User.Expiry << std::endl;
        std::cout << "Rank Level: " << auth.User.Level << std::endl;

        // 4. Fetch encrypted cloud variable
        std::string secret = auth.GetVar("client_greeting");
        std::cout << "Decrypted Variable: " << secret << std::endl;
    }
    else
    {
        std::cout << "Authentication failed: " << auth.GetLastError() << std::endl;
    }

    return 0;
}
```

To compile the C++ demo with MSVC:
- Double click `build-cpp-demo.bat`, or
- Open `BlackoutAuth.Demo.Cpp/main.cpp` in Visual Studio 2022.

---

## 🌐 Production VPS Deployment

To host Arena Auth publicly on any Windows or Linux VPS:

1. **Publish Single-File Executable**:
   ```bash
   cd BlackoutAuth.Server
   dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
   ```
2. **Reverse Proxy (Nginx / Cloudflare)**:
   Forward port `80`/`443` to `http://localhost:5000` with SSL encryption.
3. Change your client SDK's `apiUrl` parameter from `http://localhost:5000` to `https://auth.yourdomain.com`.

---

## 📁 Repository Directory Structure

```
ARENA AUTH/
├── BlackoutAuth.Server/          # ASP.NET Core Server + Web Dashboard
│   ├── Controllers/             # AuthController (SDK API) & AdminController (Dashboard)
│   ├── Data/                    # SQLite EF Core DbContext & Models
│   ├── Services/                # CryptoService (AES-256/HMAC) & DiscordService
│   ├── wwwroot/                 # Stealth Dark Web Dashboard (HTML5, Tailwind, Chart.js)
│   │   ├── index.html           # Full Admin Management Interface
│   │   ├── css/style.css        # Custom Cyber Obsidian Theme
│   │   └── js/dashboard.js      # Real-time Admin Controller Script
│   └── Program.cs
│
├── BlackoutAuth.SDK.CSharp/      # C# Client Class Library
│   ├── ArenaApp.cs           # Main SDK Class
│   ├── Security/                # Native HWID Hashing & Anti-Debugging
│   └── Models/                  # Response Models
│
├── BlackoutAuth.Demo.CSharp/     # Ready-to-Run C# Console Demo
│   └── Program.cs
│
├── BlackoutAuth.SDK.Cpp/         # C++ Native SDK
│   └── include/
│       └── BlackoutAuth.hpp     # Zero-dependency WinHTTP + BCrypt single header
│
├── BlackoutAuth.Demo.Cpp/        # Ready-to-Compile C++ Console Demo
│   ├── main.cpp
│   └── BlackoutDemoCpp.exe      # Pre-compiled Windows 64-bit Binary
│
├── BlackoutAuth.sln             # Visual Studio Solution
├── build-cpp-demo.bat           # 1-Click C++ Demo Compiler
├── start-server.bat             # 1-Click Server Runner
└── README.md                    # This documentation file
```
