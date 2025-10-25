using Harry.Controls;
using Harry.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

using static System.Net.Mime.MediaTypeNames;

namespace Harry
{
    public static class Logger
    {
        public static bool Enabled = false;
        private static void Log(string text, LogLevel level)
        {
            if (!Enabled) return;

            // TODO: kinda bad
            Task.Run(async () =>
            {
                using var db = new HarryDBC();
                db.ChangeTracker.AutoDetectChangesEnabled = false;
                db.Log.Add(new DbLog() { Text = text, CreatedAt = DateTime.Now, Type = level });
                await db.SaveChangesAsync();
            });
        }

        public static void Key(string text)
        {
            Log(text, LogLevel.Keypresses);
        }

        public static void Debug(string text)
        {
            Log(text, LogLevel.Debug);
        }

        public static void Info(string text)
        {
            Log(text, LogLevel.Info);
        }

        public static void Warning(string text)
        {
            Log(text, LogLevel.Warning);
        }

        public static void Error(Exception ex, string additionalInfo = null)
        {
            var message =
                $"{additionalInfo ?? string.Empty}\n" +
                $"\n{ex.GetType()}:{ex.Message}\n" +
                $"\n{ex.StackTrace}";

            Error(message.SplitLinesOnLimit(1024)[0]);
        }

        public static void Error(string text)
        {
            Log(text.SplitLinesOnLimit(1024)[0], LogLevel.Error);
        }

        public static void Fatal(string text)
        {
            Log(text, LogLevel.Fatal);
        }
    }
}
