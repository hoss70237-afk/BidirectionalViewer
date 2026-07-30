// File: MainForm.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;

namespace BidirectionalViewer
{
    public class MainForm : Form
    {
        private TextBox _textArea;
        private Button _btnSend, _btnCopy, _btnPaste, _btnDelete, _btnSaveTxt, _btnSavePy;
        private Label _lblCaptureTitle, _lblCaptureRegion, _lblAppTitle, _lblAppStatus;
        private Button _btnSelectRegion;
        private Button[] _appButtons;
        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _statusLabel;

        private AppConfig _config;
        private ScreenCaptureManager _capture;
        private HttpServer _server;

        public MainForm()
        {
            _config = AppConfig.Load();
            _capture = new ScreenCaptureManager();

            InitializeUi();
            ApplyConfigToUi();
            StartServer();

            this.FormClosing += MainForm_FormClosing;
        }

        // ---- UI構築 ----

        private void InitializeUi()
        {
            this.Text = "双方向メッセージビューア";
            this.Width = 640;
            this.Height = 560;
            this.StartPosition = FormStartPosition.Manual;
            this.MinimumSize = new Size(560, 480);

            // テキストエリア
            _textArea = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                AcceptsReturn = true,
                AcceptsTab = true,
                WordWrap = false,
                Location = new Point(12, 12),
                Size = new Size(600, 180),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                Font = new Font("Consolas", 10F)
            };
            this.Controls.Add(_textArea);

            // 操作ボタン行
            int by = 200;
            _btnSend    = MakeButton("送信",      12,  by, 70);
            _btnCopy    = MakeButton("コピー",     88,  by, 70);
            _btnPaste   = MakeButton("ペースト",   164, by, 70);
            _btnDelete  = MakeButton("削除",      240, by, 70);
            _btnSaveTxt = MakeButton("保存(.txt)", 330, by, 90);
            _btnSavePy  = MakeButton("保存(.py)",  426, by, 90);

            _btnSend.Click    += (s, e) => OnSend();
            _btnCopy.Click    += (s, e) => OnCopy();
            _btnPaste.Click   += (s, e) => OnPaste();
            _btnDelete.Click  += (s, e) => OnDelete();
            _btnSaveTxt.Click += (s, e) => OnSave(".txt");
            _btnSavePy.Click  += (s, e) => OnSave(".py");

            foreach (var b in new[] { _btnSend, _btnCopy, _btnPaste, _btnDelete, _btnSaveTxt, _btnSavePy })
            {
                b.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                this.Controls.Add(b);
            }

            // 区切り線1
            this.Controls.Add(MakeSeparator(235));

            // キャプチャ範囲設定
            _lblCaptureTitle = new Label
            {
                Text = "画面キャプチャ範囲設定",
                Font = new Font(this.Font, FontStyle.Bold),
                Location = new Point(12, 248),
                AutoSize = true
            };
            this.Controls.Add(_lblCaptureTitle);

            _btnSelectRegion = MakeButton("範囲選択", 12, 272, 90);
            _btnSelectRegion.Click += (s, e) => OnSelectRegion();
            this.Controls.Add(_btnSelectRegion);

            _lblCaptureRegion = new Label
            {
                Text = "未設定",
                Location = new Point(112, 278),
                AutoSize = true
            };
            this.Controls.Add(_lblCaptureRegion);

            // 区切り線2
            this.Controls.Add(MakeSeparator(308));

            // アプリ登録
            _lblAppTitle = new Label
            {
                Text = "アプリ登録 (スマホから起動)",
                Font = new Font(this.Font, FontStyle.Bold),
                Location = new Point(12, 320),
                AutoSize = true
            };
            this.Controls.Add(_lblAppTitle);

            _appButtons = new Button[6];
            for (int i = 0; i < 6; i++)
            {
                int number = i + 1;
                var btn = MakeButton(number.ToString(), 12 + i * 50, 344, 42);
                btn.Click += (s, e) => OnRegisterApp(number);
                _appButtons[i] = btn;
                this.Controls.Add(btn);
            }

            _lblAppStatus = new Label
            {
                Text = "(未登録)",
                Location = new Point(12, 380),
                Size = new Size(600, 40),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoEllipsis = true
            };
            this.Controls.Add(_lblAppStatus);

            // ステータスバー
            _statusStrip = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel("サーバー準備中...");
            _statusStrip.Items.Add(_statusLabel);
            this.Controls.Add(_statusStrip);
        }

        private Button MakeButton(string text, int x, int y, int w)
        {
            return new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, 28)
            };
        }

        private Label MakeSeparator(int y)
        {
            return new Label
            {
                BorderStyle = BorderStyle.Fixed3D,
                Location = new Point(12, y),
                Size = new Size(600, 2),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
        }

        // ---- config反映 ----

        private void ApplyConfigToUi()
        {
            // ウィンドウ位置
            if (_config.window_location != null)
            {
                var loc = new Point(_config.window_location.X, _config.window_location.Y);
                // 画面内に収まるか簡易チェック
                Rectangle vs = SystemInformation.VirtualScreen;
                if (vs.Contains(new Rectangle(loc, new Size(50, 50))))
                {
                    this.Location = loc;
                }
                else
                {
                    this.StartPosition = FormStartPosition.CenterScreen;
                }
            }
            else
            {
                this.StartPosition = FormStartPosition.CenterScreen;
            }

            UpdateCaptureRegionLabel();
            UpdateAppStatusLabel();
        }

        private void UpdateCaptureRegionLabel()
        {
            if (_config.capture_region != null && _config.capture_region.Length == 4)
            {
                int[] r = _config.capture_region;
                _lblCaptureRegion.Text = string.Format("({0}, {1}) - ({2}, {3})", r[0], r[1], r[2], r[3]);
            }
            else
            {
                _lblCaptureRegion.Text = "未設定";
            }
        }

        private void UpdateAppStatusLabel()
        {
            var sb = new StringBuilder();
            for (int i = 1; i <= 6; i++)
            {
                string key = i.ToString();
                if (_config.registered_apps.ContainsKey(key) &&
                    !string.IsNullOrEmpty(_config.registered_apps[key]))
                {
                    string name = Path.GetFileName(_config.registered_apps[key]);
                    sb.Append(string.Format("{0}: {1}  ", i, name));
                }
            }
            _lblAppStatus.Text = sb.Length > 0 ? sb.ToString().TrimEnd() : "(未登録)";
        }

        // ---- HTTPサーバー起動 ----

        private void StartServer()
        {
            var callbacks = new ServerCallbacks
            {
                SetText = SetTextThreadSafe,
                GetText = GetTextThreadSafe,
                GetCaptureRegion = () => _config.capture_region,
                GetRegisteredAppPath = GetRegisteredAppPath
            };

            _server = new HttpServer(_capture, callbacks);
            try
            {
                _server.Start();
                _statusLabel.Text = string.Format("サーバー稼働中: http://<このPCのIP>:5000/  (待受 +:5000)");
            }
            catch (Exception ex)
            {
                Logger.LogException("StartServer", ex);
                _statusLabel.Text = "サーバー起動失敗（error.log参照）";
                MessageBox.Show(
                    "HTTPサーバーの起動に失敗しました。\n" +
                    "管理者権限での実行、ポート5000の空き、ファイアウォール設定を確認してください。\n\n" +
                    ex.Message,
                    "起動エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // ---- スレッドセーフなGUIアクセス（HTTPスレッドから呼ばれる）----

        private void SetTextThreadSafe(string text)
        {
            if (_textArea.InvokeRequired)
            {
                _textArea.Invoke(new Action<string>(SetTextThreadSafe), text);
                return;
            }
            _textArea.Text = text ?? string.Empty;
        }

        private string GetTextThreadSafe()
        {
            if (_textArea.InvokeRequired)
            {
                return (string)_textArea.Invoke(new Func<string>(GetTextThreadSafe));
            }
            return _textArea.Text;
        }

        private string GetRegisteredAppPath(int number)
        {
            string key = number.ToString();
            string path;
            if (_config.registered_apps.TryGetValue(key, out path))
            {
                return path;
            }
            return null;
        }

        // ---- ボタン動作 ----

        private void OnSend()
        {
            try
            {
                var payload = new System.Web.Script.Serialization.JavaScriptSerializer()
                    .Serialize(new Dictionary<string, object> { { "text", _textArea.Text } });
                byte[] data = Encoding.UTF8.GetBytes(payload);

                var req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:5000/input");
                req.Method = "POST";
                req.ContentType = "application/json; charset=utf-8";
                req.ContentLength = data.Length;
                using (var stream = req.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }
                using (var resp = (HttpWebResponse)req.GetResponse())
                {
                    _statusLabel.Text = "送信完了: " + (int)resp.StatusCode;
                }
            }
            catch (Exception ex)
            {
                Logger.LogException("OnSend", ex);
                _statusLabel.Text = "送信失敗（error.log参照）";
            }
        }

        private void OnCopy()
        {
            string text = _textArea.SelectionLength > 0 ? _textArea.SelectedText : _textArea.Text;
            if (!string.IsNullOrEmpty(text))
            {
                Clipboard.SetText(text);
            }
        }

        private void OnPaste()
        {
            if (Clipboard.ContainsText())
            {
                string clip = Clipboard.GetText();
                int pos = _textArea.SelectionStart;
                int len = _textArea.SelectionLength;
                string current = _textArea.Text;
                _textArea.Text = current.Substring(0, pos) + clip + current.Substring(pos + len);
                _textArea.SelectionStart = pos + clip.Length;
                _textArea.SelectionLength = 0;
            }
        }

        private void OnDelete()
        {
            if (_textArea.SelectionLength > 0)
            {
                int pos = _textArea.SelectionStart;
                string current = _textArea.Text;
                _textArea.Text = current.Remove(pos, _textArea.SelectionLength);
                _textArea.SelectionStart = pos;
            }
            else
            {
                _textArea.Clear();
            }
        }

        private void OnSave(string extension)
        {
            List<string> history = extension == ".py" ? _config.history_py : _config.history_txt;

            using (var dlg = new SaveDialogForm(extension, history))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    string path = dlg.SelectedPath;
                    try
                    {
                        // BOMなしUTF-8で保存
                        File.WriteAllText(path, _textArea.Text, new UTF8Encoding(false));
                        AppConfig.AddHistory(history, path);
                        _config.Save();
                        _statusLabel.Text = "保存完了: " + path;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException("OnSave", ex);
                        MessageBox.Show(
                            "保存に失敗しました。\n" + ex.Message,
                            "保存エラー",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void OnSelectRegion()
        {
            this.Hide();
            try
            {
                using (var overlay = new OverlayForm())
                {
                    if (overlay.ShowDialog() == DialogResult.OK && overlay.SelectedRegion != null)
                    {
                        _config.capture_region = overlay.SelectedRegion;
                        _config.Save();
                        UpdateCaptureRegionLabel();
                    }
                }
            }
            finally
            {
                this.Show();
                this.Activate();
            }
        }

        private void OnRegisterApp(int number)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "実行可能ファイル (*.exe;*.bat;*.cmd;*.lnk)|*.exe;*.bat;*.cmd;*.lnk|すべてのファイル (*.*)|*.*";
                dlg.Title = string.Format("アプリ {0} を登録", number);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _config.registered_apps[number.ToString()] = dlg.FileName;
                    _config.Save();
                    UpdateAppStatusLabel();
                }
            }
        }

        // ---- 終了処理 ----

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                // ウィンドウ位置を確実に保存（最小化・最大化時は通常位置を使う）
                Point loc = this.WindowState == FormWindowState.Normal
                    ? this.Location
                    : this.RestoreBounds.Location;
                _config.window_location = new WindowLocation { X = loc.X, Y = loc.Y };
                _config.Save();
            }
            catch (Exception ex)
            {
                Logger.LogException("FormClosing", ex);
            }
            finally
            {
                if (_server != null) _server.Dispose();
                if (_capture != null) _capture.Dispose();
            }
        }
    }
}
