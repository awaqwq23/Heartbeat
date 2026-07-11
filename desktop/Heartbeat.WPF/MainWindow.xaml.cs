using System.Windows;
using System.Windows.Controls;
using Heartbeat.WPF.ViewModels;

namespace Heartbeat.WPF
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // 最小化到托盘而不退出。窗口只隐藏不销毁（App.ShowMainWindow 按 IsLoaded 复用本实例），
            // 因此 ViewModel 必须保持订阅——在此 Dispose 会让托盘重开后的窗口绑定死掉的 VM，
            // 日志与当前应用显示冻结。VM 与窗口同寿命，随进程退出一并释放。
            e.Cancel = true;
            Hide();
        }

        private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.ScrollToEnd();
            }
        }
    }
}