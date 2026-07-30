// File: OverlayForm.cs
using System;
using System.Drawing;
using System.Windows.Forms;

namespace BidirectionalViewer
{
    /// <summary>
    /// プライマリ全画面を覆う半透明オーバーレイ。
    /// 左ドラッグで赤枠を描画し、離した時点で領域を正規化して確定する。
    /// Escでキャンセル。
    /// </summary>
    public class OverlayForm : Form
    {
        private Point _start;
        private Point _current;
        private bool _dragging;

        /// <summary>
        /// 確定した領域 [x1,y1,x2,y2]（左上・右下に正規化済み）。未確定なら null。
        /// </summary>
        public int[] SelectedRegion { get; private set; }

        public OverlayForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.Bounds = Screen.PrimaryScreen.Bounds;
            this.TopMost = true;
            this.BackColor = Color.Black;
            this.Opacity = 0.30;
            this.Cursor = Cursors.Cross;
            this.DoubleBuffered = true;
            this.KeyPreview = true;

            this.MouseDown += OnMouseDown;
            this.MouseMove += OnMouseMove;
            this.MouseUp += OnMouseUp;
            this.Paint += OnPaint;
            this.KeyDown += OnKeyDown;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                SelectedRegion = null;
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            }
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                _start = e.Location;
                _current = e.Location;
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragging)
            {
                _current = e.Location;
                this.Invalidate();
            }
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !_dragging)
            {
                return;
            }
            _dragging = false;
            _current = e.Location;

            // クライアント座標 → スクリーン座標へ変換
            Point p1Screen = this.PointToScreen(_start);
            Point p2Screen = this.PointToScreen(_current);

            int x1 = Math.Min(p1Screen.X, p2Screen.X);
            int y1 = Math.Min(p1Screen.Y, p2Screen.Y);
            int x2 = Math.Max(p1Screen.X, p2Screen.X);
            int y2 = Math.Max(p1Screen.Y, p2Screen.Y);

            // サイズ0の選択は無効（キャンセル扱い）
            if (x2 - x1 < 2 || y2 - y1 < 2)
            {
                SelectedRegion = null;
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                return;
            }

            SelectedRegion = new[] { x1, y1, x2, y2 };
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void OnPaint(object sender, PaintEventArgs e)
        {
            if (!_dragging)
            {
                return;
            }

            int x = Math.Min(_start.X, _current.X);
            int y = Math.Min(_start.Y, _current.Y);
            int w = Math.Abs(_current.X - _start.X);
            int h = Math.Abs(_current.Y - _start.Y);

            using (var pen = new Pen(Color.Red, 3))
            {
                e.Graphics.DrawRectangle(pen, x, y, w, h);
            }
        }
    }
}
