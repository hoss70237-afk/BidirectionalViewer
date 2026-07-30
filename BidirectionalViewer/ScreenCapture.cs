// File: ScreenCapture.cs
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace BidirectionalViewer
{
    /// <summary>
    /// 画面キャプチャと2系統（領域用/全画面用）のPNGバッファを管理する。
    /// バッファへのアクセスはすべて lock で保護する。
    /// </summary>
    internal sealed class ScreenCaptureManager : IDisposable
    {
        private readonly object _lock = new object();

        // 領域キャプチャ用バッファ（PNGバイト列）
        private byte[] _regionPng;
        // 全画面キャプチャ用バッファ（PNGバイト列）
        private byte[] _fullscreenPng;

        /// <summary>
        /// 指定領域（左上・右下に正規化済みの [x1,y1,x2,y2]）をキャプチャし、
        /// 領域用バッファへPNGとして保持する。
        /// 領域が画面外の場合は仮想スクリーン範囲にクランプする。
        /// </summary>
        public void CaptureRegion(int[] region)
        {
            if (region == null || region.Length != 4)
            {
                throw new ArgumentException("capture_region が不正です。");
            }

            int x1 = Math.Min(region[0], region[2]);
            int y1 = Math.Min(region[1], region[3]);
            int x2 = Math.Max(region[0], region[2]);
            int y2 = Math.Max(region[1], region[3]);

            Rectangle virtualBounds = SystemInformation.VirtualScreen;

            // 仮想スクリーン範囲にクランプ
            x1 = Clamp(x1, virtualBounds.Left, virtualBounds.Right);
            x2 = Clamp(x2, virtualBounds.Left, virtualBounds.Right);
            y1 = Clamp(y1, virtualBounds.Top, virtualBounds.Bottom);
            y2 = Clamp(y2, virtualBounds.Top, virtualBounds.Bottom);

            int width = x2 - x1;
            int height = y2 - y1;

            if (width <= 0 || height <= 0)
            {
                throw new ArgumentException("キャプチャ領域のサイズが無効です。");
            }

            byte[] png = CaptureToPng(new Rectangle(x1, y1, width, height));

            lock (_lock)
            {
                _regionPng = png;
            }
        }

        /// <summary>
        /// プライマリモニタ全体をキャプチャし、全画面用バッファへPNGとして保持する。
        /// </summary>
        public void CaptureFullscreen()
        {
            Rectangle bounds = Screen.PrimaryScreen.Bounds;
            byte[] png = CaptureToPng(bounds);

            lock (_lock)
            {
                _fullscreenPng = png;
            }
        }

        /// <summary>
        /// 領域用バッファのPNGを取得。無ければ null。
        /// </summary>
        public byte[] GetRegionPng()
        {
            lock (_lock)
            {
                return _regionPng;
            }
        }

        /// <summary>
        /// 全画面用バッファのPNGを取得。無ければ null。
        /// </summary>
        public byte[] GetFullscreenPng()
        {
            lock (_lock)
            {
                return _fullscreenPng;
            }
        }

        /// <summary>
        /// 指定矩形をキャプチャしPNGバイト列へ変換する。
        /// Bitmap・Graphics・MemoryStream はすべて確実に破棄する。
        /// </summary>
        private static byte[] CaptureToPng(Rectangle rect)
        {
            using (Bitmap bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(rect.Left, rect.Top, 0, 0, rect.Size, CopyPixelOperation.SourceCopy);
                }

                using (MemoryStream ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    return ms.ToArray();
                }
            }
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _regionPng = null;
                _fullscreenPng = null;
            }
        }
    }
}
