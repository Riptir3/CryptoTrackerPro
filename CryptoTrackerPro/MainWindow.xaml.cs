using System.Windows;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.ComponentModel;

namespace CryptoTrackerPro
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;

            IntPtr hWnd = new WindowInteropHelper(this).EnsureHandle();
            int True = 1;
            DwmSetWindowAttribute(hWnd, 20, ref True, Marshal.SizeOf(typeof(int)));
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            OpenApp();
        }

        private void MyNotifyIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            OpenApp();
        }
        private void OpenApp()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}