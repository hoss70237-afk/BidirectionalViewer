// File: AppConfig.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace BidirectionalViewer
{
    /// <summary>
    /// %APPDATA%\BidirectionalViewer 配下のパスを一元管理する。
    /// </summary>
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

    /// <summary>
    /// エラーログをファイルに追記する簡易ロガー。
    /// </summary>
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
                // ログ失敗は握りつぶす
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
                    // ログ失敗は握りつぶす
                }
            }
        }
    }

    /// <summary>
    /// ウィンドウ位置。
    /// </summary>
    public class WindowLocation
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    /// <summary>
    /// config.json にマッピングされる設定オブジェクト。
    /// </summary>
    public class AppConfig
    {
        public List<string> history_txt { get; set; }
        public List<string> history_py { get; set; }
        public WindowLocation window_location { get; set; }
        public int[] capture_region { get; set; }
        public Dictionary<string, string> registered_apps { get; set; }

        public AppConfig()
        {
            // 欠損時フォールバック用のデフォルト値
            history_txt = new List<string>();
            history_py = new List<string>();
            window_location = null;      // null の場合は既定位置
            capture_region = null;       // null の場合は未設定
            registered_apps = new Dictionary<string, string>();
        }

        private static readonly object _saveLock = new object();

        /// <summary>
        /// config.json を読み込む。存在しない・壊れている場合はデフォルトを返す。
        /// </summary>
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

                // 旧フォーマット・欠損キーのフォールバック
                if (loaded == null)
                {
                    return new AppConfig();
                }
                if (loaded.history_txt == null) loaded.history_txt = new List<string>();
                if (loaded.history_py == null) loaded.history_py = new List<string>();
                if (loaded.registered_apps == null) loaded.registered_apps = new Dictionary<string, string>();

                // capture_region が不正長なら未設定扱い
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

        /// <summary>
        /// config.json へ保存する。
        /// </summary>
        public void Save()
        {
            lock (_saveLock)
            {
                try
                {
                    var serializer = new JavaScriptSerializer();
                    string json = serializer.Serialize(this);
                    // 可読性のため軽く整形（任意）
                    File.WriteAllText(AppPaths.ConfigFile, json, new UTF8Encoding(false));
                }
                catch (Exception ex)
                {
                    Logger.LogException("AppConfig.Save", ex);
                }
            }
        }

        /// <summary>
        /// 履歴リストへ追加（最新を先頭、重複削除、最大10件）。
        /// </summary>
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
