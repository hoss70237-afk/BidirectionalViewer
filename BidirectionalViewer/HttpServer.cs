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
    internal sealed class ServerCallbacks
    {
        public Action<string> SetText;
        public Func<string> GetText;
        public Func<int[]> GetCaptureRegion;
        public Func<int, string> GetRegisteredAppPath;
        
        // ウィンドウを最前面に呼び出す
        public Action ActivateWindow;
        // 公開中のファイルパスを取得
        public Func<string> GetHostedFilePath;
        // スマホからアップロードされたファイルを受け取る
        public Action<string, byte[]> OnFileUploaded;
    }

    internal sealed class HttpServer : IDisposable
    {
        private const int Port = 5000;
        private const string Prefix = "http://+:5000/";

        private readonly HttpListener _listener = new HttpListener();
        private readonly ScreenCaptureManager _capture;
        private readonly ServerCallbacks _callbacks;
        
        // 大きなファイルも受け取れるようMaxJsonLengthを最大化
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        private Thread _listenThread;
        private volatile bool _running;

        public HttpServer(ScreenCaptureManager capture, ServerCallbacks callbacks)
        {
            _capture = capture;
            _callbacks = callbacks;
            _listener.Prefixes.Add(Prefix);
        }

        public void Start()
        {
            EnsureFirewallRule();
            try { _listener.Start(); }
            catch (HttpListenerException ex)
            {
                Logger.LogException("HttpServer.Start", ex);
                throw;
            }
            _running = true;
            _listenThread = new Thread(ListenLoop) { IsBackground = true, Name = "HttpServerThread" };
            _listenThread.Start();
        }

        private void ListenLoop()
        {
            while (_running)
            {
                HttpListenerContext context;
                try { context = _listener.GetContext(); }
                catch (Exception)
                {
                    if (!_running) break;
                    continue;
                }
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try { HandleRequest(context); }
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

            res.AddHeader("Access-Control-Allow-Origin", "*");
            res.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            res.AddHeader("Access-Control-Allow-Headers", "Content-Type");

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
                case "/ping:GET": HandlePing(res); break;
                case "/input:POST": HandleInput(req, res); break;
                case "/state:GET": HandleState(res); break;
                case "/capture_once:POST": HandleCaptureOnce(res); break;
                case "/screen:GET": HandleScreen(res); break;
                case "/capture_fullscreen:POST": HandleCaptureFullscreen(res); break;
                case "/fullscreen:GET": HandleFullscreen(res); break;
                case "/get_mouse_position:GET": HandleGetMousePosition(res); break;
                case "/mouse:POST": HandleMouse(req, res); break;
                case "/launch_app:POST": HandleLaunchApp(req, res); break;
                case "/activate:GET": HandleActivate(res); break;
                case "/download:GET": HandleDownload(res); break;
                case "/upload:POST": HandleUpload(req, res); break;
                default: SendError(res, 404, "not found"); break;
            }
        }

        private void HandlePing(HttpListenerResponse res)
        {
            SendJson(res, 200, new Dictionary<string, object> { { "status", "ok" } });
        }

        private void HandleActivate(HttpListenerResponse res)
        {
            if (_callbacks.ActivateWindow != null) _callbacks.ActivateWindow();
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
            if (_callbacks.SetText != null) _callbacks.SetText(text);
            SendJson(res, 200, new Dictionary<string, object> { { "status", "ok" } });
        }

        private void HandleState(HttpListenerResponse res)
        {
            string text = _callbacks.GetText != null ? _callbacks.GetText() : string.Empty;
            string hostedPath = _callbacks.GetHostedFilePath != null ? _callbacks.GetHostedFilePath() : null;
            string hostedFile = string.IsNullOrEmpty(hostedPath) ? "" : Path.GetFileName(hostedPath);

            SendJson(res, 200, new Dictionary<string, object> 
            { 
                { "text", text ?? string.Empty },
                { "hosted_file", hostedFile }
            });
        }

        private void HandleDownload(HttpListenerResponse res)
        {
            string path = _callbacks.GetHostedFilePath != null ? _callbacks.GetHostedFilePath() : null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                SendError(res, 404, "file not found");
                return;
            }
            try
            {
                byte[] data = File.ReadAllBytes(path);
                res.StatusCode = 200;
                res.ContentType = "application/octet-stream";
                string encoded = Uri.EscapeDataString(Path.GetFileName(path));
                res.AddHeader("Content-Disposition", "attachment; filename*=UTF-8''" + encoded);
                res.ContentLength64 = data.Length;
                using (var os = res.OutputStream) { os.Write(data, 0, data.Length); }
                res.Close();
            }
            catch (Exception ex)
            {
                Logger.LogException("HandleDownload", ex);
                SendError(res, 500, "file read error");
            }
        }

        private void HandleUpload(HttpListenerRequest req, HttpListenerResponse res)
        {
            var body = ReadJsonBody(req);
            if (body == null || !body.ContainsKey("filename") || !body.ContainsKey("data"))
            {
                SendError(res, 400, "invalid payload");
                return;
            }
            try
            {
                string filename = Convert.ToString(body["filename"]);
                byte[] data = Convert.FromBase64String(Convert.ToString(body["data"]));
                
                if (_callbacks.OnFileUploaded != null) _callbacks.OnFileUploaded(filename, data);
                if (_callbacks.ActivateWindow != null) _callbacks.ActivateWindow();
                
                SendJson(res, 200, new Dictionary<string, object> { { "status", "ok" } });
            }
            catch (Exception ex)
            {
                Logger.LogException("HandleUpload", ex);
                SendError(res, 500, "failed to decode base64");
            }
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
            catch (ArgumentException ex) { SendError(res, 400, ex.Message); }
        }

        private void HandleScreen(HttpListenerResponse res)
        {
            byte[] png = _capture.GetRegionPng();
            if (png == null) { SendError(res, 404, "no region image"); return; }
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
            if (png == null) { SendError(res, 404, "no fullscreen image"); return; }
            SendPng(res, png);
        }

        private void HandleGetMousePosition(HttpListenerResponse res)
        {
            NativeMethods.POINT p = NativeMethods.GetMousePosition();
            SendJson(res, 200, new Dictionary<string, object> { { "x", p.X }, { "y", p.Y } });
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
                case "click": NativeMethods.LeftClick(x, y); break;
                case "double": NativeMethods.DoubleClick(x, y); break;
                case "right": NativeMethods.RightClick(x, y); break;
                case "move": NativeMethods.MoveTo(x, y); break;
                case "scroll_up": NativeMethods.ScrollUp(); break;
                case "scroll_down": NativeMethods.ScrollDown(); break;
                default: SendError(res, 400, "unknown action"); return;
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
            string path = _callbacks.GetRegisteredAppPath != null ? _callbacks.GetRegisteredAppPath(number) : null;
            if (string.IsNullOrEmpty(path)) { SendError(res, 400, "app not registered"); return; }
            if (!File.Exists(path)) { SendError(res, 404, "app file not found"); return; }
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

        private void SendJson(HttpListenerResponse res, int statusCode, object obj)
        {
            string json = _serializer.Serialize(obj);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            res.StatusCode = statusCode;
            res.ContentType = "application/json; charset=utf-8";
            res.ContentLength64 = buffer.Length;
            using (var os = res.OutputStream) { os.Write(buffer, 0, buffer.Length); }
            res.Close();
        }

        private void SendPng(HttpListenerResponse res, byte[] png)
        {
            res.StatusCode = 200;
            res.ContentType = "image/png";
            res.ContentLength64 = png.Length;
            using (var os = res.OutputStream) { os.Write(png, 0, png.Length); }
            res.Close();
        }

        private void SendError(HttpListenerResponse res, int statusCode, string message)
        {
            SendJson(res, statusCode, new Dictionary<string, object> { { "status", "error" }, { "message", message } });
        }

        private void TrySendError(HttpListenerContext context, int statusCode, string message)
        {
            try { SendError(context.Response, statusCode, message); } catch { }
        }

        private Dictionary<string, object> ReadJsonBody(HttpListenerRequest req)
        {
            try
            {
                if (!req.HasEntityBody) return new Dictionary<string, object>();
                using (var reader = new StreamReader(req.InputStream, Encoding.UTF8))
                {
                    string json = reader.ReadToEnd();
                    if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, object>();
                    return _serializer.Deserialize<Dictionary<string, object>>(json);
                }
            }
            catch (Exception ex) { Logger.LogException("ReadJsonBody", ex); return null; }
        }

        private static int ToInt(object value)
        {
            if (value == null) return 0;
            try { return Convert.ToInt32(value); } catch { return 0; }
        }

        private static void EnsureFirewallRule()
        {
            const string ruleName = "BidirectionalViewer Port 5000";
            try
            {
                var checkPsi = new ProcessStartInfo { FileName = "netsh", Arguments = string.Format("advfirewall firewall show rule name=\"{0}\"", ruleName), UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
                using (var proc = Process.Start(checkPsi))
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();
                    if (output.IndexOf(ruleName, StringComparison.OrdinalIgnoreCase) >= 0) return;
                }
                var addPsi = new ProcessStartInfo { FileName = "netsh", Arguments = string.Format("advfirewall firewall add rule name=\"{0}\" dir=in action=allow protocol=TCP localport={1}", ruleName, Port), UseShellExecute = false, CreateNoWindow = true };
                using (var proc = Process.Start(addPsi)) { proc.WaitForExit(); }
            }
            catch (Exception ex) { Logger.LogException("EnsureFirewallRule", ex); }
        }

        public void Stop() { _running = false; try { _listener.Stop(); } catch { } }
        public void Dispose() { Stop(); try { _listener.Close(); } catch { } }
    }
}
