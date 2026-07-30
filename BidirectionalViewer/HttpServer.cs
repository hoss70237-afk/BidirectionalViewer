// File: HttpServer.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace BidirectionalViewer
{
    /// <summary>
    /// メイン側に処理を委譲するためのコールバック集約クラス。
    /// UIスレッドへのディスパッチは各コールバック実装側（MainForm）で行う。
    /// </summary>
    internal sealed class ServerCallbacks
    {
        // GUIテキストボックスへ文字列を設定
        public Action<string> SetText;
        // GUIテキストボックスの現在文字列を取得
        public Func<string> GetText;
        // 現在の capture_region を取得（未設定なら null）
        public Func<int[]> GetCaptureRegion;
        // 登録アプリのパスを取得（未登録なら null）
        public Func<int, string> GetRegisteredAppPath;
    }

    internal sealed class HttpServer : IDisposable
    {
        private const int Port = 5000;
        private const string Prefix = "http://+:5000/";

        private readonly HttpListener _listener = new HttpListener();
        private readonly ScreenCaptureManager _capture;
        private readonly ServerCallbacks _callbacks;
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();
        private Thread _listenThread;
        private volatile bool _running;

        public HttpServer(ScreenCaptureManager capture, ServerCallbacks callbacks)
        {
            _capture = capture;
            _callbacks = callbacks;
            _listener.Prefixes.Add(Prefix);
        }

        /// <summary>
        /// サーバーを開始する。ファイアウォール規則の自動追加も試みる。
        /// </summary>
        public void Start()
        {
            EnsureFirewallRule();

            try
            {
                _listener.Start();
            }
            catch (HttpListenerException ex)
            {
                Logger.LogException("HttpServer.Start", ex);
                throw;
            }

            _running = true;
            _listenThread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "HttpServerThread"
            };
            _listenThread.Start();
        }

        private void ListenLoop()
        {
            while (_running)
            {
                HttpListenerContext context;
                try
                {
                    context = _listener.GetContext();
                }
                catch (Exception)
                {
                    // Stop() 時に例外が出るので running を見て抜ける
                    if (!_running) break;
                    continue;
                }

                // リクエストごとにスレッドプールで処理
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        HandleRequest(context);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException("HttpServer.HandleRequest", ex);
                        TrySendError(context, 500, "internal error");
                    }
                });
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            var req = context.Request;
            var res = context.Response;

            // 共通CORSヘッダー
            res.AddHeader("Access-Control-Allow-Origin", "*");
            res.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            res.AddHeader("Access-Control-Allow-Headers", "Content-Type");

            // CORSプリフライト
            if (req.HttpMethod == "OPTIONS")
            {
                res.StatusCode = 200;
                res.Close();
                return;
            }

            string path = req.Url.AbsolutePath.TrimEnd('/');
            if (path.Length == 0) path = "/";
            string method = req.HttpMethod;

            switch (path + ":" + method)
            {
                case "/ping:GET":
                    HandlePing(res);
                    break;
                case "/input:POST":
                    HandleInput(req, res);
                    break;
                case "/state:GET":
                    HandleState(res);
                    break;
                case "/capture_once:POST":
                    HandleCaptureOnce(res);
                    break;
                case "/screen:GET":
                    HandleScreen(res);
                    break;
                case "/capture_fullscreen:POST":
                    HandleCaptureFullscreen(res);
                    break;
                case "/fullscreen:GET":
                    HandleFullscreen(res);
                    break;
                case "/get_mouse_position:GET":
                    HandleGetMousePosition(res);
                    break;
                case "/mouse:POST":
                    HandleMouse(req, res);
                    break;
                case "/launch_app:POST":
                    HandleLaunchApp(req, res);
                    break;
                default:
                    SendError(res, 404, "not found");
                    break;
            }
        }

        // ---- 各エンドポイント ----

        private void HandlePing(HttpListenerResponse res)
        {
            SendJson(res, 200, new Dictionary<string, object> { { "status", "ok" } });
        }

        private void HandleInput(HttpListenerRequest req, HttpListenerResponse res)
        {
            var body = ReadJsonBody(req);
            if (body == null || !body.ContainsKey("text"))
            {
                SendError(res, 400, "invalid json: 'text' required");
                return;
            }

            string text = body["text"] as string ?? Convert.ToString(body["text"]);
            if (_callbacks.SetText != null)
            {
                _callbacks.SetText(text);
            }
            SendJson(res, 200, new Dictionary<string, object> { { "status", "ok" } });
        }

        private void HandleState(HttpListenerResponse res)
        {
            string text = _callbacks.GetText != null ? _callbacks.GetText() : string.Empty;
            SendJson(res, 200, new Dictionary<string, object> { { "text", text ?? string.Empty } });
        }

        private void HandleCaptureOnce(HttpListenerResponse res)
        {
            int[] region = _callbacks.GetCaptureRegion != null ? _callbacks.GetCaptureRegion() : null;
            if (region == null || region.Length != 4)
            {
                SendError(res, 400, "capture_region is not set");
                return;
            }

            try
            {
                _capture.CaptureRegion(region);
                SendJson(res, 200, new Dictionary<string, object> { { "status", "ok" } });
            }
            catch (ArgumentException ex)
            {
                SendError(res, 400, ex.Message);
            }
        }

        private void HandleScreen(HttpListenerResponse res)
        {
            byte[] png = _capture.GetRegionPng();
            if (png == null)
            {
                SendError(res, 404, "no region image");
                return;
            }
            SendPng(res, png);
        }

        private void HandleCaptureFullscreen(HttpListenerResponse res)
        {
            _capture.CaptureFullscreen();
            SendJson(res, 200, new Dictionary<string, object> { { "status", "ok" } });
        }

        private void HandleFullscreen(HttpListenerResponse res)
        {
            byte[] png = _capture.GetFullscreenPng();
            if (png == null)
            {
                SendError(res, 404, "no fullscreen image");
                return;
            }
            SendPng(res, png);
        }

        private void HandleGetMousePosition(HttpListenerResponse res)
        {
            NativeMethods.POINT p = NativeMethods.GetMousePosition();
            SendJson(res, 200, new Dictionary<string, object>
            {
                { "x", p.X },
                { "y", p.Y }
            });
        }

        private void HandleMouse(HttpListenerRequest req, HttpListenerResponse res)
        {
            var body = ReadJsonBody(req);
            if (body == null || !body.ContainsKey("action"))
            {
                SendError(res, 400, "invalid json: 'action' required");
                return;
            }

            string action = Convert.ToString(body["action"]);
            int x = body.ContainsKey("x") ? ToInt(body["x"]) : 0;
            int y = body.ContainsKey("y") ? ToInt(body["y"]) : 0;

            switch (action)
            {
                case "click":
                    NativeMethods.LeftClick(x, y);
                    break;
                case "double":
                    NativeMethods.DoubleClick(x, y);
                    break;
                case "right":
                    NativeMethods.RightClick(x, y);
                    break;
                case "move":
                    NativeMethods.MoveTo(x, y);
                    break;
                case "scroll_up":
                    NativeMethods.ScrollUp();
                    break;
                case "scroll_down":
                    NativeMethods.ScrollDown();
                    break;
                default:
                    SendError(res, 400, "unknown action: " + action);
                    return;
            }

            SendJson(res, 200, new Dictionary<string, object> { { "status", "ok" } });
        }

        private void HandleLaunchApp(HttpListenerRequest req, HttpListenerResponse res)
        {
            var body = ReadJsonBody(req);
            if (body == null || !body.ContainsKey("app_number"))
            {
                SendError(res, 400, "invalid json: 'app_number' required");
                return;
            }

            int number = ToInt(body["app_number"]);
            if (number < 1 || number > 6)
            {
                SendError(res, 400, "app_number out of range (1-6)");
                return;
            }

            string path = _callbacks.GetRegisteredAppPath != null
                ? _callbacks.GetRegisteredAppPath(number)
                : null;

            if (string.IsNullOrEmpty(path))
            {
                SendError(res, 400, "app not registered");
                return;
            }

            if (!File.Exists(path))
            {
                SendError(res, 404, "app file not found");
                return;
            }

            try
            {
                Process.Start(path);
                SendJson(res, 200, new Dictionary<string, object> { { "status", "ok" } });
            }
            catch (Exception ex)
            {
                Logger.LogException("HandleLaunchApp", ex);
                SendError(res, 500, "failed to start process");
            }
        }

        // ---- レスポンスヘルパー ----

        private void SendJson(HttpListenerResponse res, int statusCode, object obj)
        {
            string json = _serializer.Serialize(obj);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            res.StatusCode = statusCode;
            res.ContentType = "application/json; charset=utf-8";
            res.ContentLength64 = buffer.Length;
            using (var os = res.OutputStream)
            {
                os.Write(buffer, 0, buffer.Length);
            }
            res.Close();
        }

        private void SendPng(HttpListenerResponse res, byte[] png)
        {
            res.StatusCode = 200;
            res.ContentType = "image/png";
            res.ContentLength64 = png.Length;
            using (var os = res.OutputStream)
            {
                os.Write(png, 0, png.Length);
            }
            res.Close();
        }

        private void SendError(HttpListenerResponse res, int statusCode, string message)
        {
            SendJson(res, statusCode, new Dictionary<string, object>
            {
                { "status", "error" },
                { "message", message }
            });
        }

        private void TrySendError(HttpListenerContext context, int statusCode, string message)
        {
            try
            {
                SendError(context.Response, statusCode, message);
            }
            catch
            {
                // 応答済みなどで失敗しても無視
            }
        }

        // ---- リクエスト解析ヘルパー ----

        private Dictionary<string, object> ReadJsonBody(HttpListenerRequest req)
        {
            try
            {
                if (!req.HasEntityBody) return new Dictionary<string, object>();

                using (var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8))
                {
                    string json = reader.ReadToEnd();
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        return new Dictionary<string, object>();
                    }
                    return _serializer.Deserialize<Dictionary<string, object>>(json);
                }
            }
            catch (Exception ex)
            {
                Logger.LogException("ReadJsonBody", ex);
                return null; // 不正JSON
            }
        }

        private static int ToInt(object value)
        {
            if (value == null) return 0;
            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return 0;
            }
        }

        // ---- ファイアウォール自動許可 ----

        private static void EnsureFirewallRule()
        {
            const string ruleName = "BidirectionalViewer Port 5000";
            try
            {
                // 既存規則の確認
                var checkPsi = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = string.Format(
                        "advfirewall firewall show rule name=\"{0}\"", ruleName),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };

                using (var proc = Process.Start(checkPsi))
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();
                    if (output.IndexOf(ruleName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return; // 既に存在
                    }
                }

                // 規則追加
                var addPsi = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = string.Format(
                        "advfirewall firewall add rule name=\"{0}\" dir=in action=allow protocol=TCP localport={1}",
                        ruleName, Port),
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(addPsi))
                {
                    proc.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                // 失敗しても致命エラーとしない（手順書で手動許可を案内）
                Logger.LogException("EnsureFirewallRule", ex);
            }
        }

        public void Stop()
        {
            _running = false;
            try
            {
                _listener.Stop();
            }
            catch { }
        }

        public void Dispose()
        {
            Stop();
            try
            {
                _listener.Close();
            }
            catch { }
        }
    }
}
