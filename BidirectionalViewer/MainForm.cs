// File: MainForm.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BidirectionalViewer
{
    public class MainForm : Form
    {
        private TextBox _textArea;
        private Panel _bottomPanel;
        private Panel _appPanel;
        private Button _btnSend, _btnCopy, _btnPaste, _btnDelete, _btnSaveTxt, _btnSavePy;
        private Label _lblCaptureRegion, _lblAppTitle, _lblAppStatus;
        private Button _btnSelectRegion, _btnAppToggle;
        private Button[] _appButtons;
        
        // ファイル送受信関連
        private Button _btnHostFile, _btnHostFileClear, _btnSaveReceived;
        private Label _lblHostedFile, _lblReceivedFile;
        private string _hostedFilePath;
        private string _receivedFileName;
        private byte[] _receivedFileData;

        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _statusLabel;
        private NotifyIcon _notifyIcon;

        private AppConfig _config;
        private ScreenCaptureManager _capture;
        private HttpServer _server;
        private Timer _bgFlashTimer;

        public MainForm()
        {
            _config = AppConfig.Load();
            _capture = new ScreenCaptureManager();

            InitializeUi();
            ApplyConfigToUi();
            StartServer();

            this.FormClosing += MainForm_FormClosing;
            this.Resize += MainForm_Resize;
        }

        // ---- UI構築 ----
        private void InitializeUi()
        {
            this.Text = "双方向メッセージビューア";
            this.Width = 640;
            this.Height = 440; // 初期状態（アプリパネル非表示）の高さ
            this.StartPosition = FormStartPosition.Manual;
            this.MinimumSize = new Size(580, 400);

            // 実行ファイルに埋め込まれたアイコンを取得して適用
            Icon appIcon = null;
            try
            {
                appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                appIcon = SystemIcons.Application; // 取得失敗時のフォールバック
            }

            this.Icon = appIcon;

            // NotifyIcon (タスクトレイ)
            _notifyIcon = new NotifyIcon
            {
                Icon = appIcon,
                Visible = true,
                Text = "双方向メッセージビューア"
            };
            _notifyIcon.DoubleClick += (s, e) => RestoreWindow();

            // テキストエリア
            _textArea = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                AcceptsReturn = true,
                AcceptsTab = true,
                WordWrap = true,
                Location = new Point(12, 12),
                Size = new Size(600, 240),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                Font = new Font("Consolas", 10F)
            };
            this.Controls.Add(_textArea);

            // 下部パネル (閉じた状態の高さ 114)
            _bottomPanel = new Panel
            {
                Location = new Point(12, 260),
                Size = new Size(600, 114),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(_bottomPanel);

            // -- 下部パネル内の配置 --
            int by = 0;
            _btnSend    = MakeButton("送信",      0,   by, 70);
            _btnCopy    = MakeButton("コピー",     76,  by, 70);
            _btnPaste   = MakeButton("ペースト",   152, by, 70);
            _btnDelete  = MakeButton("削除",      228, by, 70);
            _btnSaveTxt = MakeButton("保存(.txt)", 318, by, 90);
            _btnSavePy  = MakeButton("保存(.py)",  414, by, 90);

            _btnSend.Click    += (s, e) => OnSend();
            _btnCopy.Click    += (s, e) => OnCopy();
            _btnPaste.Click   += (s, e) => OnPaste();
            _btnDelete.Click  += (s, e) => OnDelete();
            _btnSaveTxt.Click += (s, e) => OnSave(".txt");
            _btnSavePy.Click  += (s, e) => OnSave(".py");

            by = 38;
            _btnSelectRegion = MakeButton("範囲選択", 0, by, 90);
            _btnSelectRegion.Click += (s, e) => OnSelectRegion();
            _lblCaptureRegion = new Label { Text = "未設定", Location = new Point(96, by + 5), AutoSize = true };
            
            _btnAppToggle = MakeButton("アプリ設定", 414, by, 90);
            _btnAppToggle.Click += (s, e) => ToggleAppPanel(); // サイズ自動調整メソッドを呼び出す

            by = 76;
            _btnHostFile = MakeButton("PCファイル公開", 0, by, 110);
            _btnHostFile.Click += (s, e) => OnHostFile();
            
            _btnHostFileClear = MakeButton("×", 114, by, 26);
            _btnHostFileClear.Click += (s, e) => { _hostedFilePath = null; _lblHostedFile.Text = "未公開"; };

            _lblHostedFile = new Label { Text = "未公開", Location = new Point(144, by + 5), AutoSize = true, MaximumSize = new Size(160, 20), AutoEllipsis = true };

            _btnSaveReceived = MakeButton("受信ファイル保存", 318, by, 110);
            _btnSaveReceived.Enabled = false;
            _btnSaveReceived.Click += (s, e) => OnSaveReceivedFile();
            _lblReceivedFile = new Label { Text = "なし", Location = new Point(434, by + 5), AutoSize = true, MaximumSize = new Size(160, 20), AutoEllipsis = true };

            by = 114;
            var sep = new Label { BorderStyle = BorderStyle.Fixed3D, Location = new Point(0, by), Size = new Size(600, 2), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

            by = 126;
            // アプリ登録パネル (非表示)
            _appPanel = new Panel { Location = new Point(0, by), Size = new Size(600, 100), Visible = false, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _lblAppTitle = new Label { Text = "アプリ登録 (スマホから起動)", Font = new Font(this.Font, FontStyle.Bold), Location = new Point(0, 0), AutoSize = true };
            _appPanel.Controls.Add(_lblAppTitle);

            _appButtons = new Button[6];
            for (int i = 0; i < 6; i++)
            {
                int number = i + 1;
                var btn = MakeButton(number.ToString(), i * 50, 24, 42);
                btn.Click += (s, e) => OnRegisterApp(number);
                _appButtons[i] = btn;
                _appPanel.Controls.Add(btn);
            }
            _lblAppStatus = new Label { Text = "(未登録)", Location = new Point(0, 60), Size = new Size(600, 40), AutoEllipsis = true, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _appPanel.Controls.Add(_lblAppStatus);

            _bottomPanel.Controls.AddRange(new Control[] {
                _btnSend, _btnCopy, _btnPaste, _btnDelete, _btnSaveTxt, _btnSavePy,
                _btnSelectRegion, _lblCaptureRegion, _btnAppToggle,
                _btnHostFile, _btnHostFileClear, _lblHostedFile, _btnSaveReceived, _lblReceivedFile,
                sep, _appPanel
            });

            // ステータスバー
            _statusStrip = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel("サーバー準備中...");
            _statusStrip.Items.Add(_statusLabel);
            this.Controls.Add(_statusStrip);
        }

        private Button MakeButton(string text, int x, int y, int w)
        {
            return new Button { Text = text, Location = new Point(x, y), Size = new Size(w, 28) };
        }

        // アプリパネル表示/非表示時のウィンドウサイズ伸縮
        private void ToggleAppPanel()
        {
            // サイズ変更時にTextBoxが追従して伸び縮みしないように、一時的にBottomアンカーを外す
            _textArea.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _bottomPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            int adjustHeight = 112; // アプリパネルの高さ分

            if (!_appPanel.Visible)
            {
                _appPanel.Visible = true;
                this.Height += adjustHeight;
                _bottomPanel.Height += adjustHeight;
            }
            else
            {
                _appPanel.Visible = false;
                this.Height -= adjustHeight;
                _bottomPanel.Height -= adjustHeight;
            }

            // アンカーを元に戻す
            _textArea.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            _bottomPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        }

        // ---- 最小化時・復帰処理 ----
        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.ShowInTaskbar = false;
                this.Hide();
            }
        }

        private void RestoreWindow()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(RestoreWindow));
                return;
            }
            if (!this.Visible) this.Show();
            this.ShowInTaskbar = true;
            if (this.WindowState == FormWindowState.Minimized) this.WindowState = FormWindowState.Normal;
            
            this.Activate();
            this.TopMost = true;
            this.TopMost = false;
        }

        // ---- config反映 ----
        private void ApplyConfigToUi()
        {
            if (_config.window_location != null)
            {
                var loc = new Point(_config.window_location.X, _config.window_location.Y);
                if (SystemInformation.VirtualScreen.Contains(new Rectangle(loc, new Size(50, 50))))
                    this.Location = loc;
                else
                    this.StartPosition = FormStartPosition.CenterScreen;
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
                _lblCaptureRegion.Text = string.Format("({0}, {1}) - ({2}, {3})", _config.capture_region[0], _config.capture_region[1], _config.capture_region[2], _config.capture_region[3]);
            else
                _lblCaptureRegion.Text = "未設定";
        }

        private void UpdateAppStatusLabel()
        {
            var sb = new StringBuilder();
            for (int i = 1; i <= 6; i++)
            {
                string key = i.ToString();
                if (_config.registered_apps.ContainsKey(key) && !string.IsNullOrEmpty(_config.registered_apps[key]))
                    sb.Append(string.Format("{0}: {1}  ", i, Path.GetFileName(_config.registered_apps[key])));
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
                GetRegisteredAppPath = (n) => _config.registered_apps.TryGetValue(n.ToString(), out string p) ? p : null,
                ActivateWindow = RestoreWindow,
                GetHostedFilePath = () => _hostedFilePath,
                OnFileUploaded = OnFileUploaded
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
                MessageBox.Show("HTTPサーバーの起動に失敗しました。\n" + ex.Message, "起動エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---- スレッドセーフなコールバック群 ----
        private void SetTextThreadSafe(string text)
        {
            if (_textArea.InvokeRequired)
            {
                _textArea.Invoke(new Action<string>(SetTextThreadSafe), text);
                return;
            }

            if (text != null)
            {
                // スマホからの改行(\n)をWindows標準の改行(\r\n)に統一し、正しく改行表示させる
                text = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
            }
            _textArea.Text = text ?? string.Empty;

            FlashTextAreaGreen();
            RestoreWindow(); // 受信時に最前面へ
        }

        private string GetTextThreadSafe()
        {
            if (_textArea.InvokeRequired) return (string)_textArea.Invoke(new Func<string>(GetTextThreadSafe));
            return _textArea.Text;
        }

        private void OnFileUploaded(string filename, byte[] data)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string, byte[]>(OnFileUploaded), filename, data);
                return;
            }
            _receivedFileName = filename;
            _receivedFileData = data;
            _lblReceivedFile.Text = filename;
            _btnSaveReceived.Enabled = true;
            FlashTextAreaGreen();
            RestoreWindow(); // ファイル受信時にも最前面へ
        }

        private void FlashTextAreaGreen()
        {
            if (_textArea.InvokeRequired)
            {
                _textArea.Invoke(new Action(FlashTextAreaGreen));
                return;
            }
            _textArea.BackColor = Color.LightGreen;
            if (_bgFlashTimer != null) { _bgFlashTimer.Stop(); _bgFlashTimer.Dispose(); }
            _bgFlashTimer = new Timer();
            _bgFlashTimer.Interval = 500;
            _bgFlashTimer.Tick += (s, e) =>
            {
                _textArea.BackColor = SystemColors.Window;
                if (_bgFlashTimer != null) { _bgFlashTimer.Stop(); _bgFlashTimer.Dispose(); _bgFlashTimer = null; }
            };
            _bgFlashTimer.Start();
        }

        // ---- ボタン動作・ファイル処理 ----
        private async void OnSend()
        {
            string textToSend = _textArea.Text;
            _btnSend.Enabled = false;
            try
            {
                int statusCode = 0;
                await Task.Run(() =>
                {
                    var payload = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new Dictionary<string, object> { { "text", textToSend } });
                    byte[] data = Encoding.UTF8.GetBytes(payload);
                    var req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:5000/input");
                    req.Method = "POST";
                    req.ContentType = "application/json; charset=utf-8";
                    req.ContentLength = data.Length;
                    using (var stream = req.GetRequestStream()) { stream.Write(data, 0, data.Length); }
                    using (var resp = (HttpWebResponse)req.GetResponse()) { statusCode = (int)resp.StatusCode; }
                });
                _statusLabel.Text = "送信完了: " + statusCode;
                FlashTextAreaGreen();
            }
            catch (Exception ex)
            {
                Logger.LogException("OnSend", ex);
                _statusLabel.Text = "送信失敗（error.log参照）";
            }
            finally { _btnSend.Enabled = true; }
        }

        private void OnCopy() { if (!string.IsNullOrEmpty(_textArea.Text)) Clipboard.SetText(_textArea.SelectionLength > 0 ? _textArea.SelectedText : _textArea.Text); }
        private void OnPaste() { if (Clipboard.ContainsText()) { int p = _textArea.SelectionStart; string c = Clipboard.GetText(); _textArea.Text = _textArea.Text.Insert(p, c); _textArea.SelectionStart = p + c.Length; } }
        private void OnDelete() { if (_textArea.SelectionLength > 0) _textArea.Text = _textArea.Text.Remove(_textArea.SelectionStart, _textArea.SelectionLength); else _textArea.Clear(); }

        private void OnHostFile()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "公開するファイルを選択 (スマホからDL可能になります)";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _hostedFilePath = dlg.FileName;
                    _lblHostedFile.Text = Path.GetFileName(_hostedFilePath);
                }
            }
        }

        private void OnSaveReceivedFile()
        {
            if (_receivedFileData == null || string.IsNullOrEmpty(_receivedFileName)) return;
            using (var dlg = new SaveFileDialog())
            {
                dlg.FileName = _receivedFileName;
                dlg.Title = "受信ファイルの保存";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        File.WriteAllBytes(dlg.FileName, _receivedFileData);
                        MessageBox.Show("保存しました。", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        _receivedFileData = null;
                        _receivedFileName = null;
                        _lblReceivedFile.Text = "なし";
                        _btnSaveReceived.Enabled = false;
                    }
                    catch (Exception ex) { MessageBox.Show("保存エラー: " + ex.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                }
            }
        }

        private void OnSave(string ext)
        {
            var history = ext == ".py" ? _config.history_py : _config.history_txt;
            using (var dlg = new SaveDialogForm(ext, history))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        File.WriteAllText(dlg.SelectedPath, _textArea.Text, new UTF8Encoding(false));
                        AppConfig.AddHistory(history, dlg.SelectedPath);
                        _config.Save();
                        _statusLabel.Text = "保存完了: " + dlg.SelectedPath;
                    }
                    catch (Exception ex) { MessageBox.Show("保存エラー: " + ex.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error); }
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
            finally { this.Show(); this.Activate(); }
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
                Point loc = this.WindowState == FormWindowState.Normal ? this.Location : this.RestoreBounds.Location;
                _config.window_location = new WindowLocation { X = loc.X, Y = loc.Y };
                _config.Save();
            }
            catch (Exception ex) { Logger.LogException("FormClosing", ex); }
            finally
            {
                if (_notifyIcon != null) { _notifyIcon.Dispose(); }
                if (_bgFlashTimer != null) { _bgFlashTimer.Stop(); _bgFlashTimer.Dispose(); }
                if (_server != null) _server.Dispose();
                if (_capture != null) _capture.Dispose();
            }
        }
    }
}
