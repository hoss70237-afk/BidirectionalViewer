// File: BidirectionalViewer/AppConfig.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace BidirectionalViewer
{
    internal static class AppPaths
    {
        public static string BaseDir
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BidirectionalViewer");
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                return dir;
            }
        }

        public static string ConfigFile
        {
            get { return Path.Combine(BaseDir, "config.json"); }
        }

        public static string ErrorLogFile
        {
            get { return Path.Combine(BaseDir, "error.log"); }
        }
    }

    internal static class Logger
    {
        private static readonly object _lock = new object();

        public static void LogException(string context, Exception ex)
        {
            try
            {
                string message = string.Format(
                    "[{0}] {1}: {2}",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    context,
                    ex != null ? ex.ToString() : "(null)");
                Log(message);
            }
            catch
            {
            }
        }

        public static void Log(string message)
        {
            lock (_lock)
            {
                try
                {
                    File.AppendAllText(
                        AppPaths.ErrorLogFile,
                        message + Environment.NewLine,
                        new UTF8Encoding(false));
                }
                catch
                {
                }
            }
        }
    }

    public class WindowLocation
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    public class AppConfig
    {
        public List<string> history_txt { get; set; }
        public List<string> history_py { get; set; }
        public WindowLocation window_location { get; set; }
        public int[] capture_region { get; set; }
        public Dictionary<string, string> registered_apps { get; set; }
        public Dictionary<string, bool> app_communicate { get; set; }
        public Dictionary<string, string> registered_post_apps { get; set; }

        public AppConfig()
        {
            history_txt = new List<string>();
            history_py = new List<string>();
            window_location = null;
            capture_region = null;
            registered_apps = new Dictionary<string, string>();
            app_communicate = new Dictionary<string, bool>();
            registered_post_apps = new Dictionary<string, string>();
        }

        private static readonly object _saveLock = new object();

        public static AppConfig Load()
        {
            try
            {
                if (!File.Exists(AppPaths.ConfigFile))
                {
                    return new AppConfig();
                }

                string json = File.ReadAllText(AppPaths.ConfigFile, new UTF8Encoding(false));
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new AppConfig();
                }

                var serializer = new JavaScriptSerializer();
                var loaded = serializer.Deserialize<AppConfig>(json);

                if (loaded == null)
                {
                    return new AppConfig();
                }
                if (loaded.history_txt == null) loaded.history_txt = new List<string>();
                if (loaded.history_py == null) loaded.history_py = new List<string>();
                if (loaded.registered_apps == null) loaded.registered_apps = new Dictionary<string, string>();
                if (loaded.app_communicate == null) loaded.app_communicate = new Dictionary<string, bool>();
                if (loaded.registered_post_apps == null) loaded.registered_post_apps = new Dictionary<string, string>();

                if (loaded.capture_region != null && loaded.capture_region.Length != 4)
                {
                    loaded.capture_region = null;
                }

                return loaded;
            }
            catch (Exception ex)
            {
                Logger.LogException("AppConfig.Load", ex);
                return new AppConfig();
            }
        }

        public void Save()
        {
            lock (_saveLock)
            {
                try
                {
                    var serializer = new JavaScriptSerializer();
                    string json = serializer.Serialize(this);
                    File.WriteAllText(AppPaths.ConfigFile, json, new UTF8Encoding(false));
                }
                catch (Exception ex)
                {
                    Logger.LogException("AppConfig.Save", ex);
                }
            }
        }

        public static void AddHistory(List<string> list, string path)
        {
            if (list == null || string.IsNullOrEmpty(path)) return;
            list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, path);
            while (list.Count > 10)
            {
                list.RemoveAt(list.Count - 1);
            }
        }
    }
}
