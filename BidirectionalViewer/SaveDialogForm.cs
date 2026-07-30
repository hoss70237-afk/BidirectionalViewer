// File: SaveDialogForm.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace BidirectionalViewer
{
    /// <summary>
    /// カスタム保存ダイアログ。履歴選択・参照・保存/キャンセルを提供する。
    /// 実際のファイル書き込みは呼び出し元（MainForm）が SelectedPath を使って行う。
    /// </summary>
    public class SaveDialogForm : Form
    {
        private readonly string _extension;   // ".txt" or ".py"
        private ListBox _historyList;
        private TextBox _pathBox;
        private Button _btnBrowse, _btnSave, _btnCancel;
        private Label _lblTitle, _lblHistory, _lblPath;

        /// <summary>
        /// [保存]確定時に選択されたファイルパス。
        /// </summary>
        public string SelectedPath { get; private set; }

        public SaveDialogForm(string extension, List<string> history)
        {
            _extension = extension;
            InitializeUi(history);
        }

        private void InitializeUi(List<string> history)
        {
            this.Text = string.Format("保存先の選択 ({0})", _extension);
            this.Width = 560;
            this.Height = 340;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            _lblTitle = new Label
            {
                Text = string.Format("保存先の選択 ({0})", _extension),
                Font = new Font(this.Font, FontStyle.Bold),
                Location = new Point(12, 12),
                AutoSize = true
            };
            this.Controls.Add(_lblTitle);

            _lblHistory = new Label
            {
                Text = "過去の保存先履歴 (クリックで下欄に反映):",
                Location = new Point(12, 40),
                AutoSize = true
            };
            this.Controls.Add(_lblHistory);

            _historyList = new ListBox
            {
                Location = new Point(12, 62),
                Size = new Size(520, 120),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            if (history != null)
            {
                int count = 0;
                foreach (var h in history)
                {
                    if (count >= 10) break;
                    _historyList.Items.Add(h);
                    count++;
                }
            }
            _historyList.SelectedIndexChanged += (s, e) =>
            {
                if (_historyList.SelectedItem != null)
                {
                    _pathBox.Text = _historyList.SelectedItem.ToString();
                }
            };
            this.Controls.Add(_historyList);

            _lblPath = new Label
            {
                Text = "保存するファイルパス:",
                Location = new Point(12, 192),
                AutoSize = true
            };
            this.Controls.Add(_lblPath);

            _pathBox = new TextBox
            {
                Location = new Point(12, 214),
                Size = new Size(430, 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(_pathBox);

            _btnBrowse = new Button
            {
                Text = "参照...",
                Location = new Point(452, 213),
                Size = new Size(80, 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnBrowse.Click += (s, e) => OnBrowse();
            this.Controls.Add(_btnBrowse);

            _btnSave = new Button
            {
                Text = "保存",
                Location = new Point(360, 256),
                Size = new Size(80, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            _btnSave.Click += (s, e) => OnSave();
            this.Controls.Add(_btnSave);

            _btnCancel = new Button
            {
                Text = "キャンセル",
                Location = new Point(452, 256),
                Size = new Size(80, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                DialogResult = DialogResult.Cancel
            };
            this.Controls.Add(_btnCancel);

            this.AcceptButton = _btnSave;
            this.CancelButton = _btnCancel;
        }

        private void OnBrowse()
        {
            using (var dlg = new SaveFileDialog())
            {
                if (_extension == ".py")
                {
                    dlg.Filter = "Python ファイル (*.py)|*.py|すべてのファイル (*.*)|*.*";
                    dlg.DefaultExt = "py";
                }
                else
                {
                    dlg.Filter = "テキスト ファイル (*.txt)|*.txt|すべてのファイル (*.*)|*.*";
                    dlg.DefaultExt = "txt";
                }
                dlg.AddExtension = true;

                if (!string.IsNullOrEmpty(_pathBox.Text))
                {
                    try { dlg.FileName = System.IO.Path.GetFileName(_pathBox.Text); }
                    catch { }
                }

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _pathBox.Text = dlg.FileName;
                }
            }
        }

        private void OnSave()
        {
            string path = _pathBox.Text.Trim();
            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show(
                    "保存先のファイルパスを入力してください。",
                    "入力エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            SelectedPath = path;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
