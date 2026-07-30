// File: Program.cs
using System;
using System.Threading;
using System.Windows.Forms;

namespace BidirectionalViewer
{
    internal static class Program
    {
        // 多重起動防止用の名前付きMutex
        private const string MutexName = @"Global\BidirectionalViewerMutex";
        private static Mutex _mutex;

        [STAThread]
        private static void Main()
        {
            bool createdNew;
            _mutex = new Mutex(true, MutexName, out createdNew);

            if (!createdNew)
            {
                // 既に起動している場合、既存プロセスのHTTPサーバーに復帰を要求
                try
                {
                    using (var client = new System.Net.WebClient())
                    {
                        client.DownloadString("http://127.0.0.1:5000/activate");
                    }
                }
                catch { }
                return;
            }

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    Logger.LogException("UnhandledException", e.ExceptionObject as Exception);
                };
                Application.ThreadException += (s, e) =>
                {
                    Logger.LogException("ThreadException", e.Exception);
                };

                Application.Run(new MainForm());
            }
            finally
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
            }
        }
    }
}
