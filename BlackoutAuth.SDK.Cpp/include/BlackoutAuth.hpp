#pragma once

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif

#include <windows.h>
#include <winhttp.h>
#include <bcrypt.h>
#include <wincrypt.h>
#include <iostream>
#include <string>
#include <sstream>
#include <vector>
#include <iomanip>
#include <tlhelp32.h>

#pragma comment(lib, "winhttp.lib")
#pragma comment(lib, "bcrypt.lib")
#pragma comment(lib, "crypt32.lib")

namespace Blackout
{
    struct UserData
    {
        std::string Username;
        std::string Expiry;
        int Level = 1;
        std::string Hwid;
    };

    class Utils
    {
    public:
        static std::wstring ToWide(const std::string& str)
        {
            if (str.empty()) return L"";
            int size_needed = MultiByteToWideChar(CP_UTF8, 0, &str[0], (int)str.size(), NULL, 0);
            std::wstring wstr(size_needed, 0);
            MultiByteToWideChar(CP_UTF8, 0, &str[0], (int)str.size(), &wstr[0], size_needed);
            return wstr;
        }

        static std::string ToNarrow(const std::wstring& wstr)
        {
            if (wstr.empty()) return "";
            int size_needed = WideCharToMultiByte(CP_UTF8, 0, &wstr[0], (int)wstr.size(), NULL, 0, NULL, NULL);
            std::string str(size_needed, 0);
            WideCharToMultiByte(CP_UTF8, 0, &wstr[0], (int)wstr.size(), &str[0], size_needed, NULL, NULL);
            return str;
        }

        static std::string Sha256(const std::string& input)
        {
            BCRYPT_ALG_HANDLE hAlg = NULL;
            BCRYPT_HASH_HANDLE hHash = NULL;
            std::string result = "";

            if (BCryptOpenAlgorithmProvider(&hAlg, BCRYPT_SHA256_ALGORITHM, NULL, 0) == 0)
            {
                DWORD hashObjSize = 0, dataLen = 0;
                BCryptGetProperty(hAlg, BCRYPT_OBJECT_LENGTH, (PBYTE)&hashObjSize, sizeof(DWORD), &dataLen, 0);
                std::vector<BYTE> hashObj(hashObjSize);

                if (BCryptCreateHash(hAlg, &hHash, hashObj.data(), hashObjSize, NULL, 0, 0) == 0)
                {
                    BCryptHashData(hHash, (PBYTE)input.c_str(), (ULONG)input.length(), 0);
                    BYTE hashVal[32];
                    BCryptFinishHash(hHash, hashVal, 32, 0);

                    std::stringstream ss;
                    for (int i = 0; i < 32; i++)
                        ss << std::hex << std::setw(2) << std::setfill('0') << (int)hashVal[i];
                    result = ss.str();
                    BCryptDestroyHash(hHash);
                }
                BCryptCloseAlgorithmProvider(hAlg, 0);
            }
            return result;
        }

        static std::string GenerateHwid()
        {
            std::stringstream ss;
            char computerName[MAX_COMPUTERNAME_LENGTH + 1] = { 0 };
            DWORD size = sizeof(computerName);
            GetComputerNameA(computerName, &size);
            ss << computerName << ";";

            DWORD serialNum = 0;
            char sysDrive[MAX_PATH] = { 0 };
            GetSystemDirectoryA(sysDrive, MAX_PATH);
            std::string root = std::string(sysDrive).substr(0, 3); // "C:\\"
            GetVolumeInformationA(root.c_str(), NULL, 0, &serialNum, NULL, NULL, NULL, 0);
            ss << serialNum << ";";

            SYSTEM_INFO sysInfo;
            GetSystemInfo(&sysInfo);
            ss << sysInfo.dwNumberOfProcessors << ";" << sysInfo.wProcessorArchitecture << ";";

            return Sha256(ss.str());
        }

        // Lightweight zero-dependency JSON field extractor
        static std::string ExtractJsonString(const std::string& json, const std::string& key)
        {
            std::string search = "\"" + key + "\"";
            size_t pos = json.find(search);
            if (pos == std::string::npos) return "";

            pos = json.find(':', pos);
            if (pos == std::string::npos) return "";

            size_t start = json.find('"', pos);
            if (start == std::string::npos) return "";

            size_t end = json.find('"', start + 1);
            if (end == std::string::npos) return "";

            return json.substr(start + 1, end - start - 1);
        }

        static bool ExtractJsonBool(const std::string& json, const std::string& key)
        {
            std::string search = "\"" + key + "\"";
            size_t pos = json.find(search);
            if (pos == std::string::npos) return false;

            pos = json.find(':', pos);
            if (pos == std::string::npos) return false;

            size_t truePos = json.find("true", pos);
            size_t commaPos = json.find_first_of(",}", pos);

            return (truePos != std::string::npos && (commaPos == std::string::npos || truePos < commaPos));
        }

        static int ExtractJsonInt(const std::string& json, const std::string& key)
        {
            std::string search = "\"" + key + "\"";
            size_t pos = json.find(search);
            if (pos == std::string::npos) return 0;

            pos = json.find(':', pos);
            if (pos == std::string::npos) return 0;

            while (pos < json.size() && (json[pos] == ':' || json[pos] == ' ' || json[pos] == '\t')) pos++;
            return std::atoi(&json[pos]);
        }

        // Base64 decoding
        static std::vector<BYTE> Base64Decode(const std::string& in)
        {
            DWORD outLen = 0;
            CryptStringToBinaryA(in.c_str(), (DWORD)in.length(), CRYPT_STRING_BASE64, NULL, &outLen, NULL, NULL);
            std::vector<BYTE> out(outLen);
            if (outLen > 0)
            {
                CryptStringToBinaryA(in.c_str(), (DWORD)in.length(), CRYPT_STRING_BASE64, out.data(), &outLen, NULL, NULL);
            }
            return out;
        }

        // AES-256-CBC Decryption using BCrypt
        static std::string DecryptAes256(const std::string& cipherBase64, const std::string& keyHex)
        {
            auto cipherBytes = Base64Decode(cipherBase64);
            if (cipherBytes.size() <= 16) return "";

            // Derive 32-byte key
            std::string keyHashHex = Sha256(keyHex);
            std::vector<BYTE> keyBytes(32);
            for (size_t i = 0; i < 32; i++)
            {
                std::string byteStr = keyHashHex.substr(i * 2, 2);
                keyBytes[i] = (BYTE)std::strtoul(byteStr.c_str(), NULL, 16);
            }

            std::vector<BYTE> iv(cipherBytes.begin(), cipherBytes.begin() + 16);
            std::vector<BYTE> encrypted(cipherBytes.begin() + 16, cipherBytes.end());

            BCRYPT_ALG_HANDLE hAlg = NULL;
            BCRYPT_KEY_HANDLE hKey = NULL;
            std::string decrypted = "";

            if (BCryptOpenAlgorithmProvider(&hAlg, BCRYPT_AES_ALGORITHM, NULL, 0) == 0)
            {
                BCryptSetProperty(hAlg, BCRYPT_CHAINING_MODE, (PBYTE)BCRYPT_CHAIN_MODE_CBC, sizeof(BCRYPT_CHAIN_MODE_CBC), 0);
                DWORD keyObjSize = 0, dataLen = 0;
                BCryptGetProperty(hAlg, BCRYPT_OBJECT_LENGTH, (PBYTE)&keyObjSize, sizeof(DWORD), &dataLen, 0);
                std::vector<BYTE> keyObj(keyObjSize);

                if (BCryptGenerateSymmetricKey(hAlg, &hKey, keyObj.data(), keyObjSize, keyBytes.data(), (ULONG)keyBytes.size(), 0) == 0)
                {
                    DWORD plainLen = 0;
                    BCryptDecrypt(hKey, encrypted.data(), (ULONG)encrypted.size(), NULL, iv.data(), (ULONG)iv.size(), NULL, 0, &plainLen, BCRYPT_BLOCK_PADDING);
                    std::vector<BYTE> plainBytes(plainLen + 1, 0);
                    if (BCryptDecrypt(hKey, encrypted.data(), (ULONG)encrypted.size(), NULL, iv.data(), (ULONG)iv.size(), plainBytes.data(), plainLen, &plainLen, BCRYPT_BLOCK_PADDING) == 0)
                    {
                        decrypted = std::string((char*)plainBytes.data(), plainLen);
                    }
                    BCryptDestroyKey(hKey);
                }
                BCryptCloseAlgorithmProvider(hAlg, 0);
            }

            return decrypted;
        }

        static bool DetectDebugger()
        {
            if (IsDebuggerPresent()) return true;
            BOOL remote = FALSE;
            CheckRemoteDebuggerPresent(GetCurrentProcess(), &remote);
            return remote == TRUE;
        }
    };

    class HttpClient
    {
    public:
        static std::string Post(const std::string& fullUrl, const std::string& jsonBody)
        {
            std::wstring wUrl = Utils::ToWide(fullUrl);
            URL_COMPONENTS urlComp = { 0 };
            urlComp.dwStructSize = sizeof(urlComp);
            urlComp.dwHostNameLength = (DWORD)-1;
            urlComp.dwUrlPathLength = (DWORD)-1;

            if (!WinHttpCrackUrl(wUrl.c_str(), (DWORD)wUrl.length(), 0, &urlComp))
                return "";

            std::wstring hostName(urlComp.lpszHostName, urlComp.dwHostNameLength);
            std::wstring urlPath(urlComp.lpszUrlPath, urlComp.dwUrlPathLength);

            HINTERNET hSession = WinHttpOpen(L"BlackoutAuth-CppClient/1.0", WINHTTP_ACCESS_TYPE_DEFAULT_PROXY, WINHTTP_NO_PROXY_NAME, WINHTTP_NO_PROXY_BYPASS, 0);
            if (!hSession) return "";

            HINTERNET hConnect = WinHttpConnect(hSession, hostName.c_str(), urlComp.nPort, 0);
            if (!hConnect)
            {
                WinHttpCloseHandle(hSession);
                return "";
            }

            DWORD flags = (urlComp.nScheme == INTERNET_SCHEME_HTTPS) ? WINHTTP_FLAG_SECURE : 0;
            HINTERNET hRequest = WinHttpOpenRequest(hConnect, L"POST", urlPath.c_str(), NULL, WINHTTP_NO_REFERER, WINHTTP_DEFAULT_ACCEPT_TYPES, flags);
            if (!hRequest)
            {
                WinHttpCloseHandle(hConnect);
                WinHttpCloseHandle(hSession);
                return "";
            }

            std::wstring headers = L"Content-Type: application/json\r\n";
            std::string response = "";

            if (WinHttpSendRequest(hRequest, headers.c_str(), (DWORD)headers.length(), (LPVOID)jsonBody.c_str(), (DWORD)jsonBody.length(), (DWORD)jsonBody.length(), 0))
            {
                if (WinHttpReceiveResponse(hRequest, NULL))
                {
                    DWORD bytesRead = 0;
                    char buffer[4096];
                    while (WinHttpReadData(hRequest, buffer, sizeof(buffer) - 1, &bytesRead) && bytesRead > 0)
                    {
                        buffer[bytesRead] = 0;
                        response += buffer;
                    }
                }
            }

            WinHttpCloseHandle(hRequest);
            WinHttpCloseHandle(hConnect);
            WinHttpCloseHandle(hSession);

            return response;
        }
    };

    class Auth
    {
    public:
        std::string Name;
        std::string OwnerId;
        std::string Secret;
        std::string Version;
        std::string ApiUrl;
        std::string Hwid;
        bool EnableAntiDebug;

        UserData User;
        bool IsInitialized = false;
        bool IsLoggedIn = false;

        Auth(const std::string& name, const std::string& ownerId, const std::string& secret, const std::string& version, const std::string& apiUrl = "http://localhost:5000", bool enableAntiDebug = false)
            : Name(name), OwnerId(ownerId), Secret(secret), Version(version), ApiUrl(apiUrl), EnableAntiDebug(enableAntiDebug)
        {
            Hwid = Utils::GenerateHwid();
        }

        std::string GetLastError() const { return m_lastError; }

        bool Init()
        {
            if (EnableAntiDebug && Utils::DetectDebugger())
            {
                m_lastError = "Security violation: Debugger detected.";
                return false;
            }

            std::stringstream ss;
            ss << "{\"name\":\"" << Name << "\",\"ownerId\":\"" << OwnerId << "\",\"secret\":\"" << Secret << "\",\"version\":\"" << Version << "\"}";

            std::string res = HttpClient::Post(ApiUrl + "/api/v1/auth/init", ss.str());
            if (res.empty())
            {
                m_lastError = "Failed to connect to authentication server.";
                return false;
            }

            if (Utils::ExtractJsonBool(res, "success"))
            {
                m_sessionToken = Utils::ExtractJsonString(res, "session_token");
                m_encryptionKey = Utils::ExtractJsonString(res, "encryption_key");
                IsInitialized = true;
                return true;
            }

            m_lastError = Utils::ExtractJsonString(res, "message");
            if (m_lastError.empty()) m_lastError = "Initialization rejected.";
            return false;
        }

        bool LicenseOnly(const std::string& licenseKey)
        {
            if (!EnsureInit()) return false;

            std::stringstream ss;
            ss << "{\"sessionToken\":\"" << m_sessionToken << "\",\"licenseKey\":\"" << licenseKey << "\",\"hwid\":\"" << Hwid << "\"}";

            std::string res = HttpClient::Post(ApiUrl + "/api/v1/auth/license", ss.str());
            if (Utils::ExtractJsonBool(res, "success"))
            {
                ParseUser(res);
                IsLoggedIn = true;
                return true;
            }

            m_lastError = Utils::ExtractJsonString(res, "message");
            return false;
        }

        bool Login(const std::string& username, const std::string& password)
        {
            if (!EnsureInit()) return false;

            std::stringstream ss;
            ss << "{\"sessionToken\":\"" << m_sessionToken << "\",\"username\":\"" << username << "\",\"password\":\"" << password << "\",\"hwid\":\"" << Hwid << "\"}";

            std::string res = HttpClient::Post(ApiUrl + "/api/v1/auth/login", ss.str());
            if (Utils::ExtractJsonBool(res, "success"))
            {
                ParseUser(res);
                IsLoggedIn = true;
                return true;
            }

            m_lastError = Utils::ExtractJsonString(res, "message");
            return false;
        }

        bool Register(const std::string& username, const std::string& password, const std::string& licenseKey)
        {
            if (!EnsureInit()) return false;

            std::stringstream ss;
            ss << "{\"sessionToken\":\"" << m_sessionToken << "\",\"username\":\"" << username << "\",\"password\":\"" << password << "\",\"licenseKey\":\"" << licenseKey << "\",\"hwid\":\"" << Hwid << "\"}";

            std::string res = HttpClient::Post(ApiUrl + "/api/v1/auth/register", ss.str());
            if (Utils::ExtractJsonBool(res, "success"))
            {
                ParseUser(res);
                IsLoggedIn = true;
                return true;
            }

            m_lastError = Utils::ExtractJsonString(res, "message");
            return false;
        }

        std::string GetVar(const std::string& varName)
        {
            if (!EnsureInit() || !IsLoggedIn)
            {
                m_lastError = "Must be authenticated to retrieve variables.";
                return "";
            }

            std::stringstream ss;
            ss << "{\"sessionToken\":\"" << m_sessionToken << "\",\"varName\":\"" << varName << "\"}";

            std::string res = HttpClient::Post(ApiUrl + "/api/v1/auth/var", ss.str());
            if (Utils::ExtractJsonBool(res, "success"))
            {
                std::string val = Utils::ExtractJsonString(res, "value");
                if (Utils::ExtractJsonBool(res, "encrypted"))
                {
                    return Utils::DecryptAes256(val, m_encryptionKey);
                }
                return val;
            }

            m_lastError = Utils::ExtractJsonString(res, "message");
            return "";
        }

        bool CheckSession()
        {
            if (!EnsureInit()) return false;
            std::stringstream ss;
            ss << "{\"sessionToken\":\"" << m_sessionToken << "\"}";
            std::string res = HttpClient::Post(ApiUrl + "/api/v1/auth/check", ss.str());
            return Utils::ExtractJsonBool(res, "success");
        }

        void Log(const std::string& message, const std::string& severity = "INFO")
        {
            if (!EnsureInit()) return;
            std::stringstream ss;
            ss << "{\"sessionToken\":\"" << m_sessionToken << "\",\"message\":\"" << message << "\",\"severity\":\"" << severity << "\"}";
            HttpClient::Post(ApiUrl + "/api/v1/auth/log", ss.str());
        }

    private:
        std::string m_sessionToken;
        std::string m_encryptionKey;
        std::string m_lastError;

        bool EnsureInit()
        {
            if (!IsInitialized)
            {
                m_lastError = "Client not initialized. Call Init() first.";
                return false;
            }
            return true;
        }

        void ParseUser(const std::string& json)
        {
            User.Username = Utils::ExtractJsonString(json, "username");
            User.Expiry = Utils::ExtractJsonString(json, "expiry");
            User.Level = Utils::ExtractJsonInt(json, "level");
            User.Hwid = Utils::ExtractJsonString(json, "hwid");
        }
    };
}
