using System.Windows;

namespace RxCookbook.AutoSuggest.Wpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new CustomerSearchViewModel();
        }
    }
}
