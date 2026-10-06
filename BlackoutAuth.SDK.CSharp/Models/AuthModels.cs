using System;

namespace BlackoutAuth.SDK.CSharp.Models
{
    public class UserData
    {
        public string Username { get; set; } = "";
        public string Expiry { get; set; } = "";
        public int Level { get; set; } = 1;
        public string? Hwid { get; set; }
    }

    public class AppInfo
    {
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
    }
}
