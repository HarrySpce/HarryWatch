using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Readers;
using SharpCompress.Readers.Zip;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Input;
using ZipArchive = SharpCompress.Archives.Zip.ZipArchive;

namespace Harry.Utility
{
    public static class Updater
    {
        public const int Version = 35;
        public const string VersionString = "https://limiter.report";
        private const string ManifestUrl = "";
        private static Manifest manifest;

        public static bool IsLatest()
        {
            if (manifest == null)
            {
                try
                {
                    using var wc = new WebClient();
                    var response = wc.DownloadString(ManifestUrl);

                    if (response.Length < 10)
                        return true;

                    var crypt = new Crypto();
                    manifest = crypt.Decrypt(response.Trim().ToBytes()).Deserialize<Manifest>();
                }
                catch (Exception e)
                {
                    return true;
                }
            }

            return manifest.Version <= Version;
        }

        public static bool IsProtected()
        {
            IsLatest();
            if (manifest is null)
                return true;
            return MD5.HashData(File.ReadAllBytes(Process.GetCurrentProcess().MainModule.FileName)).ToHexString() != manifest.MD5;
        }

        public static bool Update()
        {
            var name = "sw_update.zip";
            using var wc = new WebClient();
            wc.DownloadFile(manifest.DownloadUrl, name);

            var dir = Path.Combine(App.ExeDirectory, "temp");
            Directory.CreateDirectory(dir);
            try
            {
                ZipFile.ExtractToDirectory(name, dir, true);
            }
            catch (InvalidDataException)
            {
                using var fs = File.OpenRead(name);
                using var zip = ZipArchive.Open(fs, new ReaderOptions() { Password = "1234" });
                zip.WriteToDirectory(dir, new SharpCompress.Common.ExtractionOptions() { Overwrite = true, ExtractFullPath = true });
            }

            RunPatcher("update");

            return true;
        }

        public static void RunPatcher(string args = null)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "patcher.exe",
                WorkingDirectory = App.ExeDirectory,
                UseShellExecute = false,
                Arguments = args ?? string.Empty
            };

            Process.Start(startInfo);
        }
    }

    public class Manifest
    {
        public int Version { get; set; }
        public string DownloadUrl { get; set; }
        public string MD5 { get; set; }
    }
}
