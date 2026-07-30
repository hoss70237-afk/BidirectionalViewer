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
                MessageBox.Show(
                    "双方向メッセージビューアは既に起動しています。",
                    "多重起動の防止",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // 未処理例外をログに記録
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
