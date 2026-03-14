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

        private void GoldenSectionButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new GoldenSectionMethod();
            window.ShowDialog();
        }

        private void NewtonButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new NewtonMethod();
            window.ShowDialog();
        }

        private void CoordinateDescentButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new CoordinateDescentMethod();
            window.ShowDialog();
        }

        private void IntegralButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new IntegralCalculator();
            window.ShowDialog();
        }

        private void LeastSquaresButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new LeastSquaresMethod();
            window.ShowDialog();
        }

        private void SortingButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new SortingAlgorithmsWindow();
            window.ShowDialog();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}