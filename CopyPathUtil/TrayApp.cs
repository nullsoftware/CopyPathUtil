using System.Reflection;
using System.Runtime.InteropServices;

namespace CopyPathUtil
{
    public class TrayApp : Form
    {
        // ---- Win32 hotkey API ----
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 0xC0DE;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_NOREPEAT = 0x4000; // don't auto-fire when key is held
        private const uint VK_C = 0x43;

        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();

        public static bool NotifyWhenCopied { get; set; }

        private readonly NotifyIcon _tray;

        public TrayApp()
        {
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;

            _tray = new NotifyIcon();
            _tray.Icon = SystemIcons.Application;
            _tray.Text = "Copy Path Hotkey  -  Ctrl+Shift+C";
            _tray.Visible = true;

            var menu = new ContextMenuStrip();
            var header = menu.Items.Add("Copy Path Hotkey (running)");
            header.Enabled = false;
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, delegate { Application.Exit(); });
            _tray.ContextMenuStrip = menu;

            // Touching Handle forces the window handle to be created now, so
            // RegisterHotKey has a valid hWnd to post WM_HOTKEY to.
            bool ok = RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, VK_C);
            if (!ok)
            {
                Notify("Could not register Ctrl+Shift+C - another app may already use it.", ToolTipIcon.Warning);
            }
        }

        // Keep the window permanently invisible (tray-only app, no flashing form).
        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(false);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && (int)m.WParam == HOTKEY_ID)
            {
                try
                {
                    HandleHotkey();
                }
                catch (Exception ex)
                {
                    // Never let anything bubble up and kill the app.
                    try { Notify("Error: " + ex.Message, ToolTipIcon.Warning); } catch { }
                }
                return;
            }
            base.WndProc(ref m);
        }

        private void HandleHotkey()
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return;

            List<string> paths = GetSelectedExplorerPaths(fg);

            if (paths.Count > 0)
            {
                SetClipboardText(string.Join("\r\n", paths.ToArray()));

                if (NotifyWhenCopied)
                {
                    string label = paths.Count == 1
                        ? "Copied path"
                        : "Copied " + paths.Count + " paths";
                    Notify(label, ToolTipIcon.Info);
                }
            }
            else
            {
                Notify("No file selected in Explorer.", ToolTipIcon.Info);
            }
        }

        // Walk the open shell windows, find the one matching the foreground
        // window, and read its selected items' full paths. Late-bound COM via
        // reflection => no SHDocVw/Shell32 interop assembly required.
        private static List<string> GetSelectedExplorerPaths(IntPtr targetHwnd)
        {
            var result = new List<string>();
            Type shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null) return result;

            object shell = Activator.CreateInstance(shellType);
            try
            {
                object windows = Invoke(shell, "Windows");
                int count = Convert.ToInt32(Get(windows, "Count"));
                for (int i = 0; i < count; i++)
                {
                    object win = Invoke(windows, "Item", i);
                    if (win == null) continue;
                    try
                    {
                        long hwnd = Convert.ToInt64(Get(win, "HWND"));
                        if (hwnd != targetHwnd.ToInt64()) continue;

                        object doc = Get(win, "Document");
                        object selected = Invoke(doc, "SelectedItems");
                        int selCount = Convert.ToInt32(Get(selected, "Count"));
                        for (int j = 0; j < selCount; j++)
                        {
                            object item = Invoke(selected, "Item", j);
                            object path = Get(item, "Path");
                            if (path != null) result.Add(path.ToString());
                            Release(item);
                        }
                        Release(selected);
                        Release(doc);
                        return result; // matched window handled; done
                    }
                    catch
                    {
                        // Non-shell window (e.g. a browser) - ignore and continue.
                    }
                    finally
                    {
                        Release(win);
                    }
                }
                Release(windows);
            }
            finally
            {
                Release(shell);
            }
            return result;
        }

        // ---- small reflection / COM helpers ----
        private static object Invoke(object target, string member, params object[] args)
        {
            return target.GetType().InvokeMember(member, BindingFlags.InvokeMethod, null, target, args);
        }

        private static object Get(object target, string member)
        {
            return target.GetType().InvokeMember(member, BindingFlags.GetProperty, null, target, null);
        }

        private static void Release(object o)
        {
            try { if (o != null && Marshal.IsComObject(o)) Marshal.ReleaseComObject(o); }
            catch { }
        }

        private static void SetClipboardText(string text)
        {
            // The clipboard can be momentarily locked by another app - retry.
            for (int attempt = 0; attempt < 6; attempt++)
            {
                try { Clipboard.SetText(text); return; }
                catch { Thread.Sleep(40); }
            }
        }

        private void Notify(string message, ToolTipIcon icon)
        {
            try { _tray.ShowBalloonTip(1200, "Copy Path Hotkey", message, icon); }
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { UnregisterHotKey(Handle, HOTKEY_ID); } catch { }
                if (_tray != null)
                {
                    _tray.Visible = false;
                    _tray.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        [STAThread]
        public static void Main()
        {
            // Single instance: auto-start should never run two copies.
            bool createdNew;
            using (var mutex = new Mutex(true, "CopyPathHotkey_SingleInstance_Mutex", out createdNew))
            {
                if (!createdNew) return;
                Application.EnableVisualStyles();
                Application.Run(new TrayApp());
            }
        }
    }

}
