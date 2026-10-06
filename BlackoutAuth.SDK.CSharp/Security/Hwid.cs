using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace BlackoutAuth.SDK.CSharp.Security
{
    public static class Hwid
    {
        public static string Get()
        {
            var sb = new StringBuilder();

            // Machine name and user domain
            sb.Append(Environment.MachineName).Append(";");
            sb.Append(Environment.UserName).Append(";");

            // Processor count & architecture
            sb.Append(Environment.ProcessorCount).Append(";");
            sb.Append(Environment.Is64BitOperatingSystem ? "x64;" : "x86;");

            // System drive volume serial / root info
            try
            {
                string sysDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
                var driveInfo = new DriveInfo(sysDrive);
                sb.Append(driveInfo.TotalSize).Append(";");
            }
            catch
            {
                sb.Append("DISK_FALLBACK;");
            }

            // Windows folder creation date
            try
            {
                var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                var created = Directory.GetCreationTimeUtc(winDir);
                sb.Append(created.Ticks).Append(";");
            }
            catch
            {
                sb.Append("WIN_FALLBACK;");
            }

            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
