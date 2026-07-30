// File: NativeMethods.cs
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace BidirectionalViewer
{
    /// <summary>
    /// user32.dll のP/Invoke定義とマウス操作ヘルパー。
    /// </summary>
    internal static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern void mouse_event(
            uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        // mouse_event フラグ
        private const uint MOUSEEVENTF_LEFTDOWN  = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP    = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP   = 0x0010;
        private const uint MOUSEEVENTF_WHEEL     = 0x0800;

        private const uint WHEEL_DELTA = 120;

        /// <summary>
        /// 現在のカーソル座標を取得。
        /// </summary>
        public static POINT GetMousePosition()
        {
            POINT p;
            GetCursorPos(out p);
            return p;
        }

        public static void MoveTo(int x, int y)
        {
            SetCursorPos(x, y);
        }

        public static void LeftClick(int x, int y)
        {
            SetCursorPos(x, y);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        }

        public static void DoubleClick(int x, int y)
        {
            SetCursorPos(x, y);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
            // ダブルクリック判定内に収める
            Thread.Sleep(30);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        }

        public static void RightClick(int x, int y)
        {
            SetCursorPos(x, y);
            mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, UIntPtr.Zero);
            mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);
        }

        public static void ScrollUp()
        {
            mouse_event(MOUSEEVENTF_WHEEL, 0, 0, WHEEL_DELTA, UIntPtr.Zero);
        }

        public static void ScrollDown()
        {
            // -120 を unchecked で uint に渡す
            mouse_event(MOUSEEVENTF_WHEEL, 0, 0, unchecked((uint)(-(int)WHEEL_DELTA)), UIntPtr.Zero);
        }
    }
}
