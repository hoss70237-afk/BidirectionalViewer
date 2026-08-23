// File: BidirectionalViewer/MainForm.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        private void InitializeUi()
        {
            this.Text = "双方向メッセージビューア";
            this.Width = 640;
            this.Height = 440; 
            this.StartPosition = FormStartPosition.Manual;
            this.MinimumSize = new Size(580, 400);

            Icon appIcon = null;
            try { appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { appIcon = SystemIcons.Application; }
            this.Icon = appIcon;

            _notifyIcon = new NotifyIcon { Icon = appIcon, Visible = true, Text = "双方向メッセージビューア" };
            _notifyIcon.DoubleClick += (s, e) => RestoreWindow();

            _textArea = new TextBox
            {
                Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true, AcceptsTab = true, WordWrap = true,
                Location = new Point(12, 12), Size = new Size(600, 240),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                Font = new Font("Consolas", 10F)
            };
            this.Controls.Add(_textArea);

            _bottomPanel = new Panel
            {
                Location = new Point(12, 260), Size = new Size(600, 114),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(_bottomPanel);

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
            _btnAppToggle.Click += (s, e) => ToggleAppPanel();

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
            _appPanel = new Panel { Location = new Point(0, by), Size = new Size(600, 100), Visible = false, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _lblAppTitle = new Label { Text = "アプリ登録 (スマホから起動)", Font = new Font(this.Font, FontStyle.Bold), Location = new Point(0, 0), AutoSize = true };
            _appPanel.Controls.Add(_lblAppTitle);

            _appButtons = new Button[6];
            for (int i = 0; i < 6; i++)
            {
                int number = i + 1;
                int bx = i * 98;

                var btn = MakeButton(number.ToString(), bx, 24, 90);
                btn.MouseDown += (s, e) => {
                    if (e.Button == MouseButtons.Right) {
                        OnRegisterApp(number);
                    } else if (e.Button == MouseButtons.Left) {
                        OnLaunchApp(number);
                    }
                };

                var chk = new CheckBox { Text = "通信", Location = new Point(bx, 54), Size = new Size(60, 20), Cursor = Cursors.Hand };
                if (_config.app_communicate.ContainsKey(number.ToString()))
                {
                    chk.Checked = _config.app_communicate[number.ToString()];
                }
                chk.CheckedChanged += (s, e) => {
                    _config.app_communicate[number.ToString()] = chk.Checked;
                    _config.Save();
                };

                _appButtons[i] = btn;
                _appPanel.Controls.Add(btn);
                _appPanel.Controls.Add(chk);
            }
            _lblAppStatus = new Label { Text = "(未登録)", Location = new Point(0, 76), Size = new Size(600, 24), AutoEllipsis = true, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _appPanel.Controls.Add(_lblAppStatus);

            _bottomPanel.Controls.AddRange(new Control[] {
                _btnSend, _btnCopy, _btnPaste, _btnDelete, _btnSaveTxt, _btnSavePy,
                _btnSelectRegion, _lblCaptureRegion, _btnAppToggle,
                _btnHostFile, _btnHostFileClear, _lblHostedFile, _btnSaveReceived, _lblReceivedFile,
                sep, _appPanel
            });

            _statusStrip = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel("サーバー準備中...");
            _statusStrip.Items.Add(_statusLabel);
            this.Controls.Add(_statusStrip);
        }

        private Button MakeButton(string text, int x, int y, int w)
        {
            return new Button { Text = text, Location = new Point(x, y), Size = new Size(w, 28) };
        }

        private void ToggleAppPanel()
        {
            _textArea.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _bottomPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            int adjustHeight = 112;

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

            _textArea.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            _bottomPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        }

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
                    sb.Append(string.Format("{0}:{1}  ", i, Path.GetFileName(_config.registered_apps[key])));
            }
            string prefix = "[左:起動 / 右:登録] ";
            _lblAppStatus.Text = sb.Length > 0 ? prefix + sb.ToString().TrimEnd() : prefix + "(未登録)";
        }

        private void StartServer()
        {
            var callbacks = new ServerCallbacks
            {
                SetText = SetTextThreadSafe,
                GetText = GetTextThreadSafe,
                GetCaptureRegion = () => _config.capture_region,
                LaunchRegisteredApp = OnLaunchApp,
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

        private void SetTextThreadSafe(string text)
        {
            if (_textArea.InvokeRequired)
            {
                _textArea.Invoke(new Action<string>(SetTextThreadSafe), text);
                return;
            }
            if (text != null) text = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
            _textArea.Text = text ?? string.Empty;

            FlashTextAreaGreen();
            RestoreWindow();
        }

        private void AppendTextThreadSafe(string appendText)
        {
            if (_textArea.InvokeRequired)
            {
                _textArea.Invoke(new Action<string>(AppendTextThreadSafe), appendText);
                return;
            }
            if (!string.IsNullOrEmpty(appendText))
            {
                appendText = appendText.Replace("\r\n", "\n").Replace("\n", "\r\n");
                _textArea.AppendText(appendText);
                FlashTextAreaGreen();
                RestoreWindow();
            }
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
            RestoreWindow();
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
                dlg.Filter = "実行可能ファイル/スクリプト (*.exe;*.bat;*.cmd;*.lnk;*.ahk;*.py)|*.exe;*.bat;*.cmd;*.lnk;*.ahk;*.py|すべてのファイル (*.*)|*.*";
                dlg.Title = string.Format("アプリ {0} を登録", number);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _config.registered_apps[number.ToString()] = dlg.FileName;
                    _config.Save();
                    UpdateAppStatusLabel();
                }
            }
        }

        private void OnLaunchApp(int number)
        {
            string key = number.ToString();
            if (!_config.registered_apps.ContainsKey(key) || string.IsNullOrEmpty(_config.registered_apps[key]))
            {
                OnRegisterApp(number);
                return;
            }

            string path = _config.registered_apps[key];
            if (!File.Exists(path))
            {
                MessageBox.Show("ファイルが見つかりません: " + path, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            bool communicate = _config.app_communicate.ContainsKey(key) && _config.app_communicate[key];

            if (!communicate)
            {
                try { Process.Start(path); }
                catch (Exception ex) { Logger.LogException("OnLaunchApp", ex); }
            }
            else
            {
                string currentText = _textArea.Text;
                _statusLabel.Text = string.Format("アプリ {0} と通信中...", number);
                Task.Run(() => RunAppWithCommunication(path, currentText));
            }
        }

        // AutoHotkey の実行ファイルの場所をいくつか探す
        private string FindAutoHotkeyExe()
        {
            string[] possiblePaths = {
                @"C:\Program Files\AutoHotkey\v2\AutoHotkey64.exe",
                @"C:\Program Files\AutoHotkey\v2\AutoHotkey32.exe",
                @"C:\Program Files\AutoHotkey\AutoHotkeyU64.exe",
                @"C:\Program Files\AutoHotkey\AutoHotkeyU32.exe",
                @"C:\Program Files\AutoHotkey\AutoHotkeyA32.exe",
                @"C:\Program Files\AutoHotkey\AutoHotkey.exe"
            };
            foreach (var path in possiblePaths)
            {
                if (File.Exists(path)) return path;
            }
            return null;
        }

        private void RunAppWithCommunication(string exePath, string inputText)
        {
            string tempIn = null;
            string tempOut = null;
            try
            {
                tempIn = Path.GetTempFileName();
                tempOut = Path.GetTempFileName();
                
                File.WriteAllText(tempIn, inputText, new UTF8Encoding(false));
                
                var psi = new ProcessStartInfo();
                
                // .ahkファイルが指定された場合、Windowsの関連付け仕様で引数が消えるのを防ぐため、
                // AutoHotkey.exe を直接呼び出す
                if (exePath.EndsWith(".ahk", StringComparison.OrdinalIgnoreCase))
                {
                    string ahkExe = FindAutoHotkeyExe();
                    if (!string.IsNullOrEmpty(ahkExe))
                    {
                        psi.FileName = ahkExe;
                        psi.Arguments = string.Format("\"{0}\" \"{1}\" \"{2}\"", exePath, tempIn, tempOut);
                        psi.UseShellExecute = false;
                        psi.CreateNoWindow = true;
                    }
                    else
                    {
                        // 見つからない場合は関連付けに任せる（ユーザーが exe 化していることを期待）
                        psi.FileName = exePath;
                        psi.Arguments = string.Format("\"{0}\" \"{1}\"", tempIn, tempOut);
                        psi.UseShellExecute = true;
                    }
                }
                else
                {
                    psi.FileName = exePath;
                    psi.Arguments = string.Format("\"{0}\" \"{1}\"", tempIn, tempOut);
                    psi.UseShellExecute = true;
                }
                
                using (var proc = Process.Start(psi))
                {
                    if (proc != null)
                    {
                        proc.WaitForExit();
                    }
                }
                
                if (File.Exists(tempOut))
                {
                    string result = File.ReadAllText(tempOut, new UTF8Encoding(false));
                    if (!string.IsNullOrWhiteSpace(result))
                    {
                        AppendTextThreadSafe("\r\n\r\n--- 応答 ---\r\n" + result);
                    }
                }
                
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => { _statusLabel.Text = "通信完了"; }));
                }
            }
            catch (Exception ex)
            {
                Logger.LogException("RunAppWithCommunication", ex);
            }
            finally
            {
                try { if (tempIn != null && File.Exists(tempIn)) File.Delete(tempIn); } catch { }
                try { if (tempOut != null && File.Exists(tempOut)) File.Delete(tempOut); } catch { }
            }
        }

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
