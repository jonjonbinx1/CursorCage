using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace CursorLocker
{
    public class MainForm : Form
    {
        // WinAPI declarations
        [DllImport("user32.dll")] static extern bool ClipCursor(ref RECT rect);
        [DllImport("user32.dll")] static extern bool ClipCursor(IntPtr rect);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")] static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hHook);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hHook, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)]
        static extern short GetAsyncKeyState(int vKey);

        [DllImport("kernel32.dll", SetLastError = true)]

        static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        // Constants
        private const int HOTKEY_ID = 9001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint VK_L = 0x4C;

        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_ACTIVATEAPP = 0x001C;

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int VK_TAB = 0x09;
        private const int VK_MENU = 0x12;

        // Alt
        private bool altIsDown = false; // Add this field to your class
        private HookProc keyboardHookCallback;
        private IntPtr keyboardHook = IntPtr.Zero;
        private bool lockSuspended = false;



        // UI
        private ComboBox comboBoxWindows = new ComboBox();
        private Button btnRefreshList = new Button();
        private Label lblStatus = new Label();
        private NumericUpDown marginControl = new NumericUpDown();
        private ToolTip toolTip = new ToolTip();

        // State
        private readonly List<IntPtr> windowHandles = new List<IntPtr>();
        private IntPtr targetWindow = IntPtr.Zero;
        private bool isLocked = false;
        private Thread lockThread;
        private bool threadRunning = false;
        private bool appHasFocus = true;


        private HookProc mouseHookCallback;
        private IntPtr mouseHook = IntPtr.Zero;

        public MainForm()
        {
            this.Text = "Cursor Locker";
            this.Size = new Size(420, 180);

            comboBoxWindows.Location = new Point(20, 20);
            comboBoxWindows.Width = 240;
            comboBoxWindows.SelectedIndexChanged += comboBoxWindows_SelectedIndexChanged;

            btnRefreshList.Text = "Refresh List";
            btnRefreshList.Location = new Point(270, 20);
            btnRefreshList.Click += btnRefreshList_Click;

            lblStatus.Text = "Ready";
            lblStatus.Location = new Point(20, 60);
            lblStatus.AutoSize = true;

            marginControl.Location = new Point(20, 90);
            marginControl.Width = 60;
            marginControl.Minimum = 0;
            marginControl.Maximum = 100;
            marginControl.Value = 5;

            toolTip.SetToolTip(comboBoxWindows, "Select a window to lock your cursor to");
            toolTip.SetToolTip(btnRefreshList, "Refresh the list of visible windows");
            toolTip.SetToolTip(lblStatus, "Hotkey: Ctrl + Shift + L to toggle lock");
            toolTip.SetToolTip(marginControl, "Adjust margin size (in pixels)");

            this.Controls.Add(comboBoxWindows);
            this.Controls.Add(btnRefreshList);
            this.Controls.Add(lblStatus);
            this.Controls.Add(marginControl);

            PopulateWindowList();
            RegisterHotKey(this.Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_L);
        }

        private void PopulateWindowList()
        {
            windowHandles.Clear();
            comboBoxWindows.Items.Clear();

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;
                StringBuilder title = new StringBuilder(256);
                GetWindowText(hWnd, title, title.Capacity);
                string name = title.ToString();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    windowHandles.Add(hWnd);
                    comboBoxWindows.Items.Add(name);
                }
                return true;
            }, IntPtr.Zero);

            lblStatus.Text = "Select a window and press Lock (Ctrl + Shift + L)";
        }

        private void btnRefreshList_Click(object sender, EventArgs e)
        {
            PopulateWindowList();
        }

        private void comboBoxWindows_SelectedIndexChanged(object sender, EventArgs e)
        {
            targetWindow = windowHandles[comboBoxWindows.SelectedIndex];
            lblStatus.Text = $"Selected: {comboBoxWindows.SelectedItem} - Press Ctrl + Shift + L to lock";
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_ACTIVATEAPP:
                    appHasFocus = m.WParam != IntPtr.Zero;
                    break;

                case 0x0312: // Hotkey: Ctrl + Shift + L
                    isLocked = !isLocked;

                    if (isLocked)
                    {
                        lblStatus.Text = $"[Locked] Cursor confined to: {comboBoxWindows.SelectedItem}";
                        StartLockThread();

                        mouseHookCallback = MouseHookProc;
                        mouseHook = SetWindowsHookEx(WH_MOUSE_LL, mouseHookCallback, GetModuleHandle(null), 0);

                        // keyboardHookCallback = KeyboardHookProc;
                        keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, keyboardHookCallback, GetModuleHandle(null), 0);
                    }
                    else
                    {
                        UnlockMouse();
                    }
                    break;

                default:
                    // Fallback focus check (handles edge cases like multi-monitor Alt+Tab behavior)
                    if (isLocked)
                    {
                        IntPtr currentForeground = GetForegroundWindow();

                        if (currentForeground != targetWindow && !lockSuspended)
                        {
                            ClipCursor(IntPtr.Zero);
                            lockSuspended = true;
                            lblStatus.Text = "[Unlocked - focus lost]";
                        }
                        else if (currentForeground == targetWindow && lockSuspended)
                        {
                            ApplyCursorLock();
                            lockSuspended = false;
                            lblStatus.Text = $"[Locked] Cursor confined to: {comboBoxWindows.SelectedItem}";
                        }
                    }

                    break;
            }

            base.WndProc(ref m);
        }

        private void UnlockMouse()
        {
            lblStatus.Text = "Unlocked";
            StopLockThread();
            ClipCursor(IntPtr.Zero);
            UnhookMouse();
            UnhookKeyboard();
            appHasFocus = false;
            lockSuspended = false;
        }

        private void ApplyCursorLock()
        {
            if (targetWindow != IntPtr.Zero && GetWindowRect(targetWindow, out RECT rect) && !lockSuspended)
            {
                int margin = Convert.ToInt32(marginControl.Invoke(new Func<decimal>(() => marginControl.Value)));
                RECT inset = new RECT
                {
                    Left = rect.Left + margin,
                    Top = rect.Top + margin,
                    Right = rect.Right - margin,
                    Bottom = rect.Bottom - margin
                };

                ClipCursor(ref inset);
            }
        }



        private void StartLockThread()
        {
            threadRunning = true;
            lockThread = new Thread(() =>
            {
                try
                {
                    while (threadRunning)
                    {
                        if (isLocked && !lockSuspended && targetWindow != IntPtr.Zero)
                        {
                            if (GetWindowRect(targetWindow, out RECT rect))
                            {
                                int margin = marginControl.InvokeRequired
                                    ? Convert.ToInt32(marginControl.Invoke(new Func<decimal>(() => marginControl.Value)))
                                    : Convert.ToInt32(marginControl.Value);

                                RECT inset = new RECT
                                {
                                    Left = rect.Left + margin,
                                    Top = rect.Top + margin,
                                    Right = rect.Right - margin,
                                    Bottom = rect.Bottom - margin
                                };

                                ClipCursor(ref inset);
                            }
                        }
                        else
                        {
                            ClipCursor(IntPtr.Zero);
                        }

                        Thread.Sleep(10);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lock thread error:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            });

            lockThread.IsBackground = true;
            lockThread.Start();
        }


        private void StopLockThread()
        {
            threadRunning = false;
            if (lockThread != null && lockThread.IsAlive)
            {
                lockThread.Join(100);
            }
        }

        private void UnhookMouse()
        {
            if (mouseHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(mouseHook);
                mouseHook = IntPtr.Zero;
            }
        }

        private void UnhookKeyboard()
        {
            if (keyboardHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(keyboardHook);
                keyboardHook = IntPtr.Zero;
            }
        }


        private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN)
            {
                Point cursorPos = Cursor.Position;

                if (targetWindow != IntPtr.Zero && GetWindowRect(targetWindow, out RECT bounds))
                {
                    // 👆 Restore lock if suspended and click inside target window
                    if (lockSuspended &&
                        cursorPos.X >= bounds.Left && cursorPos.X <= bounds.Right &&
                        cursorPos.Y >= bounds.Top && cursorPos.Y <= bounds.Bottom)
                    {
                        AttemptDelayedRelock();
                        // // Attempt to restore focus before applying lock
                        // SetForegroundWindow(targetWindow);

                        // // Check if focus is now on target
                        // if (GetForegroundWindow() == targetWindow)
                        // {
                        //     ApplyCursorLock();
                        //     lockSuspended = false;

                        //     lblStatus.Invoke((MethodInvoker)(() =>
                        //         lblStatus.Text = $"[Locked] Cursor confined to: {comboBoxWindows.SelectedItem}"));
                        // }
                    }


                    // 🔒 Swallow clicks outside if lock is active
                    if (isLocked && !lockSuspended &&
                        (cursorPos.X < bounds.Left || cursorPos.X > bounds.Right ||
                        cursorPos.Y < bounds.Top || cursorPos.Y > bounds.Bottom))
                    {
                        return (IntPtr)1; // Eat external click
                    }
                }
            }

            return CallNextHookEx(mouseHook, nCode, wParam, lParam);
        }

        private void AttemptDelayedRelock()
        {
            System.Windows.Forms.Timer relockTimer = new System.Windows.Forms.Timer();
            relockTimer.Interval = 150; // milliseconds
            relockTimer.Tick += (s, e) =>
            {
                relockTimer.Stop();
                relockTimer.Dispose();

                if (GetForegroundWindow() == targetWindow)
                {
                    ApplyCursorLock();
                    lockSuspended = false;

                    lblStatus.Invoke((MethodInvoker)(() =>
                        lblStatus.Text = $"[Locked] Cursor confined to: {comboBoxWindows.SelectedItem}"));
                }
            };

            relockTimer.Start();
        }

        // private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        // {
        //     if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
        //     {
        //         // Detect Alt held down
        //         bool altDown = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;

        //         // If Alt is pressed and we've lost foreground focus
        //         if (altDown && isLocked && !lockSuspended && GetForegroundWindow() != targetWindow)
        //         {
        //             ClipCursor(IntPtr.Zero);
        //             lockSuspended = true;

        //             lblStatus.Invoke((MethodInvoker)(() =>
        //                 lblStatus.Text = "[Unlocked - Alt pressed + focus lost]"));
        //         }
        //     }

        //     return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        // }


        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            UnregisterHotKey(this.Handle, HOTKEY_ID);
            StopLockThread();
            UnhookMouse();
            UnhookKeyboard();
            ClipCursor(IntPtr.Zero);
            base.OnFormClosing(e);
        }
    }
}
