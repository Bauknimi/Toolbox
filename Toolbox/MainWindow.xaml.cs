using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Toolbox.PixivMeta;

namespace Toolbox
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private PixivMetaUserControl _pixivMetaControl;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void SideMenuItem_PixivMeta_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _pixivMetaControl ??= new PixivMetaUserControl();
            ContentControl.Content = _pixivMetaControl;
        }
    }
}
