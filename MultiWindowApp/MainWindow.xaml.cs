using System.Windows;

namespace MultiWindowApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void DichotomyButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new DichotomyMethod();
            window.ShowDialog();
        }

        private void LinearSystemButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new LinearSystemSolver();
            window.ShowDialog();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}