using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MultiWindowApp
{
    public partial class SortingAlgorithmsWindow : Window
    {
        public class NumberItem
        {
            public int Value { get; set; }
        }

        private ObservableCollection<NumberItem> _items;
        private readonly Random _random = new Random();

        public SortingAlgorithmsWindow()
        {
            InitializeComponent();
            _items = new ObservableCollection<NumberItem>();
            DataGridValues.ItemsSource = _items;
            CountTextBox.Text = "20";
            MaxValueTextBox.Text = "100";
            BubbleCheckBox.IsChecked = true;
            AscendingRadioButton.IsChecked = true;
        }

        private void CalculateMenuItem_Click(object sender, RoutedEventArgs e)
        {
            RunSorts();
        }

        private void ClearMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _items.Clear();
            ClearCanvases();
            StatusTextBlock.Text = string.Empty;
            BubbleTimeText.Text = string.Empty;
            InsertionTimeText.Text = string.Empty;
            ShakerTimeText.Text = string.Empty;
            QuickTimeText.Text = string.Empty;
            BogoTimeText.Text = string.Empty;
        }

        private void LoadFromCsvMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog dlg = new OpenFileDialog
                {
                    Filter = "CSV файлы (*.csv)|*.csv|Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
                    DefaultExt = ".csv",
                    Multiselect = false
                };

                if (dlg.ShowDialog() == true)
                {
                    string[] lines = System.IO.File.ReadAllLines(dlg.FileName);
                    List<int> values = new List<int>();

                    foreach (string line in lines)
                    {
                        string[] parts = line.Split(new[] { ';', ',', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string part in parts)
                        {
                            int v;
                            if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
                                values.Add(v);
                        }
                    }

                    if (values.Count == 0)
                    {
                        MessageBox.Show("В файле не найдено подходящих числовых значений.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    _items.Clear();
                    foreach (int v in values)
                        _items.Add(new NumberItem { Value = v });

                    CountTextBox.Text = values.Count.ToString();
                    MaxValueTextBox.Text = values.Max().ToString(CultureInfo.InvariantCulture);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadFromGoogleMenuItem_Click(object sender, RoutedEventArgs e)
        {
            GooglePasteWindow dialog = new GooglePasteWindow();
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string data = dialog.Data ?? string.Empty;
                    string[] lines = data.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    List<int> values = new List<int>();

                    foreach (string line in lines)
                    {
                        string[] parts = line.Split(new[] { ';', ',', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string part in parts)
                        {
                            int v;
                            if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
                                values.Add(v);
                        }
                    }

                    if (values.Count == 0)
                    {
                        MessageBox.Show("Не удалось распознать числа.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    _items.Clear();
                    foreach (int v in values)
                        _items.Add(new NumberItem { Value = v });

                    CountTextBox.Text = values.Count.ToString();
                    MaxValueTextBox.Text = values.Max().ToString(CultureInfo.InvariantCulture);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка разбора данных: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void GenerateMenuItem_Click(object sender, RoutedEventArgs e)
        {
            int count;
            int maxValue;
            if (!int.TryParse(CountTextBox.Text, out count) || count <= 0)
            {
                MessageBox.Show("Количество элементов должно быть положительным целым числом.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(MaxValueTextBox.Text, out maxValue) || maxValue <= 0)
            {
                MessageBox.Show("Максимальное значение должно быть положительным целым числом.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _items.Clear();
            for (int i = 0; i < count; i++)
            {
                int v = _random.Next(1, maxValue + 1);
                _items.Add(new NumberItem { Value = v });
            }
        }

        private void CloseMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void RunSorts()
        {
            if (!_items.Any())
            {
                MessageBox.Show("Заполните данные или сгенерируйте тестовые значения.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            List<int> baseValues = _items.Select(i => i.Value).ToList();

            if (!(BubbleCheckBox.IsChecked == true ||
                  InsertionCheckBox.IsChecked == true ||
                  ShakerCheckBox.IsChecked == true ||
                  QuickCheckBox.IsChecked == true ||
                  BogoCheckBox.IsChecked == true))
            {
                MessageBox.Show("Выберите хотя бы один алгоритм сортировки.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (BogoCheckBox.IsChecked == true && baseValues.Count > 10)
            {
                var result = MessageBox.Show(
                    $"В таблице {baseValues.Count} элементов. BOGO работает только с массивами до 10 элементов.\n\n" +
                    $"Сгенерировать новый массив из 8 элементов?",
                    "BOGO — слишком много элементов",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    int maxValue;
                    if (!int.TryParse(MaxValueTextBox.Text, out maxValue) || maxValue <= 0)
                        maxValue = 100;

                    _items.Clear();
                    for (int i = 0; i < 8; i++)
                        _items.Add(new NumberItem { Value = _random.Next(1, maxValue + 1) });

                    CountTextBox.Text = "8";
                    baseValues = _items.Select(x => x.Value).ToList();
                }
                else
                {
                    return;
                }
            }

            bool ascending = AscendingRadioButton.IsChecked == true;
            ClearCanvases();
            StatusTextBlock.Text = "Выполняется сортировка...";

            RunAllSortsAsync(baseValues, ascending);
        }

        private async void RunAllSortsAsync(List<int> baseValues, bool ascending)
        {
            var tasks = new List<Task>();

            if (BubbleCheckBox.IsChecked == true)
                tasks.Add(RunAlgorithmAsync("Bubble", baseValues, ascending, BubbleCanvas, BubbleTimeText, Brushes.SteelBlue));
            if (InsertionCheckBox.IsChecked == true)
                tasks.Add(RunAlgorithmAsync("Insertion", baseValues, ascending, InsertionCanvas, InsertionTimeText, Brushes.SeaGreen));
            if (ShakerCheckBox.IsChecked == true)
                tasks.Add(RunAlgorithmAsync("Shaker", baseValues, ascending, ShakerCanvas, ShakerTimeText, Brushes.DarkOrange));
            if (QuickCheckBox.IsChecked == true)
                tasks.Add(RunAlgorithmAsync("Quick", baseValues, ascending, QuickCanvas, QuickTimeText, Brushes.Indigo));
            if (BogoCheckBox.IsChecked == true)
                tasks.Add(RunAlgorithmAsync("Bogo", baseValues, ascending, BogoCanvas, BogoTimeText, Brushes.Crimson));

            await Task.WhenAll(tasks);
            StatusTextBlock.Text = "Сортировка завершена.";
        }

        private async Task RunAlgorithmAsync(string name, List<int> baseValues, bool ascending, Canvas canvas, TextBlock timeText, Brush brush)
        {
            int[] data = baseValues.ToArray();
            Stopwatch sw = new Stopwatch();
            sw.Start();

            switch (name)
            {
                case "Bubble":
                    await BubbleSortAsync(data, ascending, canvas, brush);
                    break;
                case "Insertion":
                    await InsertionSortAsync(data, ascending, canvas, brush);
                    break;
                case "Shaker":
                    await ShakerSortAsync(data, ascending, canvas, brush);
                    break;
                case "Quick":
                    await QuickSortAsync(data, ascending, canvas, brush);
                    break;
                case "Bogo":
                    await BogoSortAsync(data, ascending, canvas, brush);
                    break;
            }

            sw.Stop();
            timeText.Text = "Время: " + sw.ElapsedMilliseconds + " мс";
        }

        private async Task BubbleSortAsync(int[] data, bool ascending, Canvas canvas, Brush brush)
        {
            int n = data.Length;
            for (int i = 0; i < n - 1; i++)
            {
                for (int j = 0; j < n - i - 1; j++)
                {
                    if (Compare(data[j], data[j + 1], ascending) > 0)
                    {
                        int t = data[j];
                        data[j] = data[j + 1];
                        data[j + 1] = t;
                        DrawArray(canvas, data, brush);
                        await Task.Delay(15);
                    }
                }
            }
            DrawArray(canvas, data, brush);
        }

        private async Task InsertionSortAsync(int[] data, bool ascending, Canvas canvas, Brush brush)
        {
            for (int i = 1; i < data.Length; i++)
            {
                int key = data[i];
                int j = i - 1;
                while (j >= 0 && Compare(data[j], key, ascending) > 0)
                {
                    data[j + 1] = data[j];
                    j--;
                    DrawArray(canvas, data, brush);
                    await Task.Delay(15);
                }
                data[j + 1] = key;
                DrawArray(canvas, data, brush);
                await Task.Delay(15);
            }
            DrawArray(canvas, data, brush);
        }

        private async Task ShakerSortAsync(int[] data, bool ascending, Canvas canvas, Brush brush)
        {
            int left = 0;
            int right = data.Length - 1;
            while (left < right)
            {
                for (int i = left; i < right; i++)
                {
                    if (Compare(data[i], data[i + 1], ascending) > 0)
                    {
                        int t = data[i];
                        data[i] = data[i + 1];
                        data[i + 1] = t;
                        DrawArray(canvas, data, brush);
                        await Task.Delay(15);
                    }
                }
                right--;

                for (int i = right; i > left; i--)
                {
                    if (Compare(data[i - 1], data[i], ascending) > 0)
                    {
                        int t = data[i];
                        data[i] = data[i - 1];
                        data[i - 1] = t;
                        DrawArray(canvas, data, brush);
                        await Task.Delay(15);
                    }
                }
                left++;
            }
            DrawArray(canvas, data, brush);
        }

        private async Task QuickSortAsync(int[] data, bool ascending, Canvas canvas, Brush brush)
        {
            await QuickSortInnerAsync(data, 0, data.Length - 1, ascending, canvas, brush);
            DrawArray(canvas, data, brush);
        }

        private async Task QuickSortInnerAsync(int[] data, int left, int right, bool ascending, Canvas canvas, Brush brush)
        {
            if (left >= right)
                return;

            int i = left;
            int j = right;
            int pivot = data[(left + right) / 2];

            while (i <= j)
            {
                while (Compare(data[i], pivot, ascending) < 0)
                    i++;
                while (Compare(data[j], pivot, ascending) > 0)
                    j--;
                if (i <= j)
                {
                    int t = data[i];
                    data[i] = data[j];
                    data[j] = t;
                    i++;
                    j--;
                    DrawArray(canvas, data, brush);
                    await Task.Delay(10);
                }
            }

            if (left < j)
                await QuickSortInnerAsync(data, left, j, ascending, canvas, brush);
            if (i < right)
                await QuickSortInnerAsync(data, i, right, ascending, canvas, brush);
        }

        private async Task BogoSortAsync(int[] data, bool ascending, Canvas canvas, Brush brush)
        {
            DrawArray(canvas, data, brush);
            await Task.Delay(50);

            Stopwatch timeout = Stopwatch.StartNew();
            long maxTimeMs = 120000;
            int attempts = 0;

            while (!IsSorted(data, ascending))
            {
                Shuffle(data);
                attempts++;

                if (attempts % 10 == 0)
                {
                    DrawArray(canvas, data, brush);
                    await Task.Delay(5);
                }

                if (timeout.ElapsedMilliseconds > maxTimeMs)
                {
                    DrawArray(canvas, data, brush);
                    StatusTextBlock.Text = $"BOGO: таймаут 2 мин, попыток: {attempts}";
                    return;
                }
            }

            DrawArray(canvas, data, brush);
            StatusTextBlock.Text = $"BOGO: отсортировано за {attempts} попыток";
        }

        private void Shuffle(int[] data)
        {
            for (int i = data.Length - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                int t = data[i];
                data[i] = data[j];
                data[j] = t;
            }
        }

        private bool IsSorted(int[] data, bool ascending)
        {
            for (int i = 1; i < data.Length; i++)
            {
                if (Compare(data[i - 1], data[i], ascending) > 0)
                    return false;
            }
            return true;
        }

        private int Compare(int a, int b, bool ascending)
        {
            if (ascending)
                return a.CompareTo(b);
            return b.CompareTo(a);
        }

        private void DrawArray(Canvas canvas, int[] data, Brush brush)
        {
            canvas.Children.Clear();
            if (data.Length == 0)
                return;

            double width = canvas.ActualWidth;
            double height = canvas.ActualHeight;
            if (width <= 0 || height <= 0)
            {
                width = canvas.Width > 0 ? canvas.Width : 200;
                height = canvas.Height > 0 ? canvas.Height : 80;
            }

            int max = Math.Max(1, data.Max());
            double barWidth = Math.Max(2, width / data.Length);

            for (int i = 0; i < data.Length; i++)
            {
                double value = data[i];
                double barHeight = value / max * (height - 4);
                Rectangle rect = new Rectangle
                {
                    Width = barWidth - 1,
                    Height = barHeight,
                    Fill = brush
                };
                Canvas.SetLeft(rect, i * barWidth);
                Canvas.SetTop(rect, height - barHeight);
                canvas.Children.Add(rect);
            }
        }

        private void ClearCanvases()
        {
            BubbleCanvas.Children.Clear();
            InsertionCanvas.Children.Clear();
            ShakerCanvas.Children.Clear();
            QuickCanvas.Children.Clear();
            BogoCanvas.Children.Clear();
        }
    }

    public class GooglePasteWindow : Window
    {
        private TextBox _textBox;
        public string Data { get; private set; }

        public GooglePasteWindow()
        {
            Title = "Вставка данных из Google Table";
            Width = 500;
            Height = 350;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            Grid grid = new Grid
            {
                Margin = new Thickness(12)
            };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock header = new TextBlock
            {
                Text = "Вставьте столбец чисел (один столбец, одна колонка):",
                Margin = new Thickness(0, 0, 0, 8),
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            _textBox = new TextBox
            {
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas")
            };
            Grid.SetRow(_textBox, 1);
            grid.Children.Add(_textBox);

            StackPanel buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 0, 0)
            };

            Button okButton = new Button
            {
                Content = "ОК",
                Width = 80,
                Margin = new Thickness(0, 0, 8, 0)
            };
            okButton.Click += OkButton_Click;

            Button cancelButton = new Button
            {
                Content = "Отмена",
                Width = 80
            };
            cancelButton.Click += (s, e) => DialogResult = false;

            buttons.Children.Add(okButton);
            buttons.Children.Add(cancelButton);

            Grid.SetRow(buttons, 2);
            grid.Children.Add(buttons);

            Content = grid;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_textBox.Text))
            {
                MessageBox.Show("Введите или вставьте данные.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Data = _textBox.Text;
            DialogResult = true;
        }
    }
}