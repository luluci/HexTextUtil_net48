using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace HexTextUtil
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        MainWindowViewModel vm;

        public MainWindow()
        {
            try
            {
                this.vm = new MainWindowViewModel();
                InitializeComponent();

            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"アプリ起動で例外発生：{ex.Message}\r\nアプリを終了します。");
                Application.Current.Shutdown();
                return;
            }
            this.DataContext = this.vm;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private async void Window_Initialized(object sender, EventArgs e)
        {
            // InitializeComponent();の中からコールされるぽい？
            await vm.Init();
        }
    }
}
