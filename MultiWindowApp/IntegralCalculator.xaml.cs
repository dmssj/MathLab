using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NCalc;
using Expression = NCalc.Expression;

namespace MultiWindowApp
{
    public partial class IntegralCalculator : Window
    {
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isCalculating = false;
        private List<CalculationResult> _results;

        public IntegralCalculator()
        {
            InitializeComponent();
            _cancellationTokenSource = new CancellationTokenSource();
            _results = new List<CalculationResult>();
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        }

        private async void CalculateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isCalculating)
            {
                MessageBox.Show("Расчет уже выполняется. Дождитесь окончания или остановите расчет.");
                return;
            }

            if (!ValidateInputs())
                return;

            if (!GetSelectedMethods().Any())
            {
                MessageBox.Show("Выберите хотя бы один метод расчета.");
                return;
            }

            _isCalculating = true;
            _cancellationTokenSource = new CancellationTokenSource();
            StatusText.Text = "Выполняется расчет...";
            StatusText.Foreground = Brushes.Orange;
            CalculationProgress.Visibility = Visibility.Visible;
            ProgressText.Text = "Запуск методов...";

            HistoryPanel.Children.Clear();

            try
            {
                await CalculateIntegralsAsync(_cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                Log("Расчет отменен пользователем.");
                StatusText.Text = "Расчет отменен";
                StatusText.Foreground = Brushes.Red;
            }
            catch (Exception ex)
            {
                Log($"Ошибка при расчете: {ex.Message}");
                StatusText.Text = "Ошибка расчета";
                StatusText.Foreground = Brushes.Red;
                MessageBox.Show($"Ошибка при расчете: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isCalculating = false;
                CalculationProgress.Visibility = Visibility.Collapsed;
                if (StatusText.Text != "Расчет отменен" && StatusText.Text != "Ошибка расчета")
                {
                    StatusText.Text = "Расчет завершен";
                    StatusText.Foreground = Brushes.Green;
                }
                ProgressText.Text = "";
            }
        }

        private async Task CalculateIntegralsAsync(CancellationToken cancellationToken)
        {
            _results.Clear();
            ResultsPanel.Children.Clear();
            LogTextBox.Clear();
            Log("Начало расчета интеграла");

            string function = FunctionTextBox.Text.Trim();
            double a = ParseDouble(ATextBox.Text);
            double b = ParseDouble(BTextBox.Text);
            double epsilon = ParseDouble(EpsilonTextBox.Text);
            int initialN = int.Parse(NTextBox.Text);
            bool autoN = AutoNCheckBox.IsChecked ?? false;

            Log($"Функция: f(x) = {function}");
            Log($"Интервал: [{a}, {b}]");
            Log($"Точность: ε = {epsilon}");
            Log($"Начальное количество разбиений: n = {initialN}");
            Log($"Автоматический подбор n: {(autoN ? "включен" : "выключен")}");

            var tasks = new List<Task>();
            var selectedMethods = GetSelectedMethods();

            CalculationProgress.Maximum = selectedMethods.Count();
            CalculationProgress.Value = 0;

            foreach (var method in selectedMethods)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var task = Task.Run(() =>
                {
                    var stopwatch = Stopwatch.StartNew();
                    CalculationResult result;

                    try
                    {
                        result = CalculateMethod(method, function, a, b, initialN, epsilon, autoN, cancellationToken);
                        result.Time = stopwatch.Elapsed;
                    }
                    catch (Exception ex)
                    {
                        result = new CalculationResult
                        {
                            MethodName = method,
                            Error = ex.Message,
                            Time = stopwatch.Elapsed
                        };
                    }

                    return result;
                }, cancellationToken).ContinueWith(t =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (t.IsFaulted)
                        {
                            Log($"Ошибка в методе {method}: {t.Exception?.InnerException?.Message}");
                        }
                        else if (!t.IsFaulted && !t.IsCanceled && t.Result != null)
                        {
                            _results.Add(t.Result);
                            AddResultToPanel(t.Result);
                            if (autoN)
                            {
                                AddHistoryToPanel(t.Result);
                            }
                            CalculationProgress.Value++;
                            ProgressText.Text = $"Выполнено {CalculationProgress.Value} из {selectedMethods.Count()} методов";
                        }
                    });
                }, cancellationToken);

                tasks.Add(task);
            }

            await Task.WhenAll(tasks);

            await BuildPlotAsync(function, a, b, cancellationToken);

            Log($"Расчет завершен. Выполнено {_results.Count} методов.");

            if (autoN && _results.Any(r => r.Error == null))
            {
                int maxN = _results.Where(r => r.Error == null).Max(r => r.FinalN);
                Dispatcher.Invoke(() =>
                {
                    NTextBox.Text = maxN.ToString();
                    Log($"Автоматически подобранное количество разбиений: n = {maxN}");
                });
            }
        }

        private CalculationResult CalculateMethod(string method, string function,
            double a, double b, int initialN, double epsilon, bool autoN,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Func<double, double> f = x => CalculateFunction(x, function);
            int n = initialN;
            double result = 0;
            int iterations = 0;
            List<IterationHistory> history = new List<IterationHistory>();
            int finalN = n;

            if (autoN)
            {
                n = 4;

                Log($"Метод {method}: начинаем автоматический подбор n с начального значения {n}");

                List<double> resultsByN = new List<double>();

                do
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    result = CalculateIntegral(method, f, a, b, n);
                    resultsByN.Add(result);

                    history.Add(new IterationHistory
                    {
                        Iteration = iterations + 1,
                        N = n,
                        Result = result
                    });

                    if (resultsByN.Count >= 2)
                    {
                        double prevResult = resultsByN[resultsByN.Count - 2];
                        double diff = Math.Abs(result - prevResult);

                        Log($"Метод {method}: итерация {iterations + 1}, n = {n}, результат = {result:F8}, изменение = {diff:E2}");

                        if (diff < epsilon)
                        {
                            Log($"Метод {method}: достигнута точность {diff:E2} < {epsilon}, останавливаемся");
                            break;
                        }
                    }

                    n *= 2;
                    iterations++;

                    if (iterations >= 20 || n >= 1000000)
                    {
                        Log($"Метод {method}: достигнут предел итераций ({iterations}) или n ({n})");
                        break;
                    }
                } while (true);

                finalN = n;
                Log($"Метод {method}: финальный результат с n = {finalN}: {result:F8}, итераций: {iterations + 1}");
            }
            else
            {
                Log($"Метод {method}: используем фиксированное n = {n}");
                result = CalculateIntegral(method, f, a, b, n);
                history.Add(new IterationHistory
                {
                    Iteration = 1,
                    N = n,
                    Result = result
                });
                iterations = 1;
                finalN = n;
                Log($"Метод {method}: результат = {result:F8}");
            }

            return new CalculationResult
            {
                MethodName = method,
                Result = result,
                Iterations = iterations,
                FinalN = finalN,
                History = history,
                Error = null
            };
        }

        private double CalculateIntegral(string method, Func<double, double> f, double a, double b, int n)
        {
            double h = (b - a) / n;
            double sum = 0;

            switch (method)
            {
                case "RectLeft":
                    for (int i = 0; i < n; i++)
                    {
                        double x = a + i * h;
                        sum += f(x);
                    }
                    return sum * h;

                case "RectMiddle":
                    for (int i = 0; i < n; i++)
                    {
                        double x = a + (i + 0.5) * h;
                        sum += f(x);
                    }
                    return sum * h;

                case "RectRight":
                    for (int i = 1; i <= n; i++)
                    {
                        double x = a + i * h;
                        sum += f(x);
                    }
                    return sum * h;

                case "Trapezoidal":
                    sum = (f(a) + f(b)) / 2;
                    for (int i = 1; i < n; i++)
                    {
                        double x = a + i * h;
                        sum += f(x);
                    }
                    return sum * h;

                case "Simpson":
                    if (n % 2 != 0)
                        throw new ArgumentException("Для метода Симпсона количество разбиений должно быть четным");

                    sum = f(a) + f(b);
                    for (int i = 1; i < n; i++)
                    {
                        double x = a + i * h;
                        sum += (i % 2 == 0) ? 2 * f(x) : 4 * f(x);
                    }
                    return sum * h / 3;

                default:
                    throw new ArgumentException($"Неизвестный метод: {method}");
            }
        }

        private double CalculateFunction(double x, string functionText)
        {
            try
            {
                Expression expression = new Expression(functionText);
                expression.Parameters["x"] = x;

                expression.EvaluateFunction += delegate (string name, FunctionArgs args)
                {
                    if (name == "sqrt")
                        args.Result = Math.Sqrt(Convert.ToDouble(args.Parameters[0].Evaluate()));
                    else if (name == "sin")
                        args.Result = Math.Sin(Convert.ToDouble(args.Parameters[0].Evaluate()));
                    else if (name == "cos")
                        args.Result = Math.Cos(Convert.ToDouble(args.Parameters[0].Evaluate()));
                    else if (name == "tan")
                        args.Result = Math.Tan(Convert.ToDouble(args.Parameters[0].Evaluate()));
                    else if (name == "log" || name == "ln")
                        args.Result = Math.Log(Convert.ToDouble(args.Parameters[0].Evaluate()));
                    else if (name == "exp")
                        args.Result = Math.Exp(Convert.ToDouble(args.Parameters[0].Evaluate()));
                    else if (name == "abs")
                        args.Result = Math.Abs(Convert.ToDouble(args.Parameters[0].Evaluate()));
                    else if (name == "pow")
                    {
                        double baseValue = Convert.ToDouble(args.Parameters[0].Evaluate());
                        double exponent = Convert.ToDouble(args.Parameters[1].Evaluate());
                        args.Result = Math.Pow(baseValue, exponent);
                    }
                };

                object result = expression.Evaluate();

                if (result == null)
                    throw new ArgumentException("Не удалось вычислить функцию");

                double value = Convert.ToDouble(result);

                if (double.IsInfinity(value) || double.IsNaN(value))
                {
                    throw new ArgumentException("Функция не определена в этой точке");
                }

                return value;
            }
            catch (DivideByZeroException)
            {
                throw new ArgumentException("Деление на ноль");
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Ошибка в функции: {ex.Message}");
            }
        }

        private bool IsFunctionDefined(double x, string functionText)
        {
            try
            {
                CalculateFunction(x, functionText);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private IEnumerable<string> GetSelectedMethods()
        {
            var methods = new List<string>();

            if (RectLeftCheckBox.IsChecked ?? false)
                methods.Add("RectLeft");
            if (RectMiddleCheckBox.IsChecked ?? false)
                methods.Add("RectMiddle");
            if (RectRightCheckBox.IsChecked ?? false)
                methods.Add("RectRight");
            if (TrapezoidalCheckBox.IsChecked ?? false)
                methods.Add("Trapezoidal");
            if (SimpsonCheckBox.IsChecked ?? false)
                methods.Add("Simpson");

            return methods;
        }

        private bool ValidateInputs()
        {
            if (string.IsNullOrWhiteSpace(FunctionTextBox.Text))
            {
                MessageBox.Show("Введите функцию f(x).", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                FunctionTextBox.Focus();
                return false;
            }

            if (!double.TryParse(ATextBox.Text.Replace(',', '.'), out double a))
            {
                MessageBox.Show("Левая граница интервала (a) должна быть числом.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                ATextBox.Focus();
                return false;
            }

            if (!double.TryParse(BTextBox.Text.Replace(',', '.'), out double b))
            {
                MessageBox.Show("Правая граница интервала (b) должна быть числом.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                BTextBox.Focus();
                return false;
            }

            if (a >= b)
            {
                MessageBox.Show("Левая граница интервала (a) должна быть меньше правой (b).", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                ATextBox.Focus();
                return false;
            }

            if (!double.TryParse(EpsilonTextBox.Text.Replace(',', '.'), out double epsilon) || epsilon <= 0)
            {
                MessageBox.Show("Точность (ε) должна быть положительным числом.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                EpsilonTextBox.Focus();
                return false;
            }

            if (!(AutoNCheckBox.IsChecked ?? false))
            {
                if (!int.TryParse(NTextBox.Text, out int n) || n <= 0)
                {
                    MessageBox.Show("Количество разбиений (n) должно быть положительным целым числом.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                    NTextBox.Focus();
                    return false;
                }

                if ((SimpsonCheckBox.IsChecked ?? false) && n % 2 != 0)
                {
                    MessageBox.Show("Для метода Симпсона количество разбиений должно быть четным.\nИсправьте значение n или снимите выбор с метода Симпсона.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    NTextBox.Focus();
                    return false;
                }
            }

            try
            {
                string testFunction = FunctionTextBox.Text.Trim();

                double testA = a;
                double testB = b;
                double testMid = (a + b) / 2;

                double val1 = CalculateFunction(testA, testFunction);
                double val2 = CalculateFunction(testMid, testFunction);
                double val3 = CalculateFunction(testB, testFunction);

                Log($"Тест функции: f({testA}) = {val1}, f({testMid}) = {val2}, f({testB}) = {val3}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка в функции: {ex.Message}\n\nПримеры правильных функций:\n• pow(x,2)\n• sin(x)\n• exp(x)\n• x*sin(x)\n• 1/(1+pow(x,2))", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                FunctionTextBox.Focus();
                return false;
            }

            return true;
        }

        private double ParseDouble(string text)
        {
            return double.Parse(text.Replace(',', '.'), CultureInfo.InvariantCulture);
        }

        private void AddResultToPanel(CalculationResult result)
        {
            Dispatcher.Invoke(() =>
            {
                var border = new Border
                {
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 0, 3),
                    Padding = new Thickness(4),
                    Background = result.Error == null ? Brushes.White : Brushes.LightPink,
                    CornerRadius = new CornerRadius(3)
                };

                var stackPanel = new StackPanel();

                var methodText = new TextBlock
                {
                    Text = GetMethodDisplayName(result.MethodName),
                    FontWeight = FontWeights.Bold,
                    Foreground = result.Error == null ? Brushes.Black : Brushes.Red,
                    FontSize = 11,
                    Margin = new Thickness(0, 0, 0, 2)
                };
                stackPanel.Children.Add(methodText);

                if (result.Error != null)
                {
                    var errorText = new TextBlock
                    {
                        Text = $"Ошибка: {result.Error}",
                        Foreground = Brushes.Red,
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 10
                    };
                    stackPanel.Children.Add(errorText);
                }
                else
                {
                    var resultText = new TextBlock
                    {
                        Text = $"∫f(x)dx ≈ {result.Result:F6}",
                        Margin = new Thickness(0, 1, 0, 0),
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 10
                    };
                    stackPanel.Children.Add(resultText);

                    var detailsText = new TextBlock
                    {
                        Text = $"Время: {result.Time.TotalMilliseconds:F0} мс | n: {result.FinalN}",
                        Margin = new Thickness(0, 1, 0, 0),
                        FontSize = 9,
                        Foreground = Brushes.DarkGray
                    };
                    stackPanel.Children.Add(detailsText);

                    if (result.Iterations > 1)
                    {
                        var iterText = new TextBlock
                        {
                            Text = $"Итераций: {result.Iterations}",
                            Margin = new Thickness(0, 1, 0, 0),
                            FontSize = 9,
                            Foreground = Brushes.DarkSlateGray
                        };
                        stackPanel.Children.Add(iterText);
                    }
                }

                border.Child = stackPanel;
                ResultsPanel.Children.Add(border);

                Log($"Метод {GetMethodDisplayName(result.MethodName)}: {(result.Error != null ? $"Ошибка: {result.Error}" : $"результат = {result.Result:F6}, n = {result.FinalN}")}");
            });
        }

        private void AddHistoryToPanel(CalculationResult result)
        {
            if (result.History == null || result.History.Count == 0)
                return;

            Dispatcher.Invoke(() =>
            {
                var border = new Border
                {
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 0, 3),
                    Padding = new Thickness(4),
                    Background = Brushes.White,
                    CornerRadius = new CornerRadius(3)
                };

                var stackPanel = new StackPanel();

                var methodText = new TextBlock
                {
                    Text = $"{GetMethodDisplayName(result.MethodName)}:",
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 2),
                    FontSize = 11
                };
                stackPanel.Children.Add(methodText);

                foreach (var iteration in result.History.Take(5))
                {
                    var iterationText = new TextBlock
                    {
                        Text = $"Шаг {iteration.Iteration}: n={iteration.N}, ∫≈{iteration.Result:F6}",
                        FontSize = 9,
                        Margin = new Thickness(2, 1, 0, 1),
                        TextWrapping = TextWrapping.Wrap
                    };
                    stackPanel.Children.Add(iterationText);
                }

                if (result.History.Count > 5)
                {
                    var summaryText = new TextBlock
                    {
                        Text = $"... и ещё {result.History.Count - 5} итераций",
                        FontSize = 8,
                        Margin = new Thickness(2, 1, 0, 1),
                        FontStyle = FontStyles.Italic,
                        Foreground = Brushes.Gray
                    };
                    stackPanel.Children.Add(summaryText);
                }

                var finalText = new TextBlock
                {
                    Text = $"Итог: ∫≈{result.Result:F6} (n={result.FinalN}, {result.Iterations} итераций)",
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 3, 0, 0),
                    FontSize = 10,
                    Foreground = Brushes.DarkBlue
                };
                stackPanel.Children.Add(finalText);

                border.Child = stackPanel;
                HistoryPanel.Children.Add(border);
            });
        }

        private async Task BuildPlotAsync(string function, double a, double b, CancellationToken cancellationToken)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                PlotCanvas.Children.Clear();
                NoPlotText.Visibility = Visibility.Collapsed;

                double canvasWidth = PlotCanvas.ActualWidth;
                double canvasHeight = PlotCanvas.ActualHeight;

                if (canvasWidth <= 0 || canvasHeight <= 0)
                    return;

                double padding = 40;
                double plotWidth = canvasWidth - 2 * padding;
                double plotHeight = canvasHeight - 2 * padding;

                try
                {
                    int samples = 100;
                    double minY = double.MaxValue;
                    double maxY = double.MinValue;

                    for (int i = 0; i <= samples; i++)
                    {
                        double x = a + (b - a) * i / samples;
                        double y = CalculateFunction(x, function);

                        if (!double.IsInfinity(y) && !double.IsNaN(y))
                        {
                            minY = Math.Min(minY, y);
                            maxY = Math.Max(maxY, y);
                        }
                    }

                    if (minY == double.MaxValue || maxY == double.MinValue)
                    {
                        minY = -10;
                        maxY = 10;
                    }

                    if (Math.Abs(maxY - minY) < 1e-10)
                    {
                        minY -= 1;
                        maxY += 1;
                    }

                    double yRange = maxY - minY;
                    minY -= yRange * 0.1;
                    maxY += yRange * 0.1;

                    DrawAxis(padding, plotWidth, plotHeight, a, b, minY, maxY);
                    DrawFunction(function, padding, plotWidth, plotHeight, a, b, minY, maxY);

                    foreach (var result in _results.Where(r => r.Error == null && r.FinalN > 0))
                    {
                        DrawMethodPartition(result, function, padding, plotWidth, plotHeight, a, b, minY, maxY, result.MethodName);
                    }
                }
                catch (Exception ex)
                {
                    Log($"Ошибка при построении графика: {ex.Message}");
                    NoPlotText.Visibility = Visibility.Visible;
                    NoPlotText.Text = $"Ошибка построения: {ex.Message}";
                }
            });
        }

        private void DrawFunction(string function, double padding, double plotWidth,
            double plotHeight, double a, double b, double minY, double maxY)
        {
            var polyline = new System.Windows.Shapes.Polyline
            {
                Stroke = Brushes.Blue,
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round
            };

            int points = 200;
            for (int i = 0; i <= points; i++)
            {
                double x = a + (b - a) * i / points;

                try
                {
                    double y = CalculateFunction(x, function);

                    if (!double.IsInfinity(y) && !double.IsNaN(y))
                    {
                        double xPos = padding + (x - a) / (b - a) * plotWidth;
                        double yPos = padding + plotHeight - (y - minY) / (maxY - minY) * plotHeight;
                        polyline.Points.Add(new Point(xPos, yPos));
                    }
                }
                catch
                {
                }
            }

            if (polyline.Points.Count > 0)
            {
                PlotCanvas.Children.Add(polyline);
            }
        }

        private void DrawMethodPartition(CalculationResult result, string function,
            double padding, double plotWidth, double plotHeight, double a, double b,
            double minY, double maxY, string method)
        {
            if (result.FinalN <= 0)
                return;

            Color methodColor;
            switch (method)
            {
                case "RectLeft":
                    methodColor = Colors.Red;
                    break;
                case "RectMiddle":
                    methodColor = Colors.Green;
                    break;
                case "RectRight":
                    methodColor = Colors.Orange;
                    break;
                case "Trapezoidal":
                    methodColor = Colors.Purple;
                    break;
                case "Simpson":
                    methodColor = Colors.Teal;
                    break;
                default:
                    methodColor = Colors.Gray;
                    break;
            }

            double h = (b - a) / result.FinalN;
            var brush = new SolidColorBrush(methodColor) { Opacity = 0.3 };

            if (method == "Simpson")
            {
                DrawSimpsonPartition(function, padding, plotWidth, plotHeight, a, b, minY, maxY, result.FinalN, brush);
                return;
            }

            for (int i = 0; i < result.FinalN; i++)
            {
                double x1 = a + i * h;
                double x2 = x1 + h;

                double x1Pos = padding + (x1 - a) / (b - a) * plotWidth;
                double x2Pos = padding + (x2 - a) / (b - a) * plotWidth;

                try
                {
                    switch (method)
                    {
                        case "RectLeft":
                            {
                                double yLeft = CalculateFunction(x1, function);
                                if (!double.IsInfinity(yLeft) && !double.IsNaN(yLeft))
                                {
                                    double yLeftPos = padding + plotHeight - (yLeft - minY) / (maxY - minY) * plotHeight;
                                    DrawRectangle(x1Pos, yLeftPos, x2Pos, padding + plotHeight, brush);
                                }
                                break;
                            }
                        case "RectMiddle":
                            {
                                double xMid = (x1 + x2) / 2;
                                double yMid = CalculateFunction(xMid, function);
                                if (!double.IsInfinity(yMid) && !double.IsNaN(yMid))
                                {
                                    double yMidPos = padding + plotHeight - (yMid - minY) / (maxY - minY) * plotHeight;
                                    DrawRectangle(x1Pos, yMidPos, x2Pos, padding + plotHeight, brush);
                                }
                                break;
                            }
                        case "RectRight":
                            {
                                double yRight = CalculateFunction(x2, function);
                                if (!double.IsInfinity(yRight) && !double.IsNaN(yRight))
                                {
                                    double yRightPos = padding + plotHeight - (yRight - minY) / (maxY - minY) * plotHeight;
                                    DrawRectangle(x1Pos, yRightPos, x2Pos, padding + plotHeight, brush);
                                }
                                break;
                            }
                        case "Trapezoidal":
                            {
                                double y1 = CalculateFunction(x1, function);
                                double y2 = CalculateFunction(x2, function);
                                if (!double.IsInfinity(y1) && !double.IsNaN(y1) &&
                                    !double.IsInfinity(y2) && !double.IsNaN(y2))
                                {
                                    double y1Pos = padding + plotHeight - (y1 - minY) / (maxY - minY) * plotHeight;
                                    double y2Pos = padding + plotHeight - (y2 - minY) / (maxY - minY) * plotHeight;
                                    DrawTrapezoid(x1Pos, y1Pos, x2Pos, y2Pos, padding + plotHeight, brush);
                                }
                                break;
                            }
                    }
                }
                catch
                {
                }
            }
        }

        private void DrawSimpsonPartition(string function, double padding, double plotWidth,
            double plotHeight, double a, double b, double minY, double maxY, int n, Brush brush)
        {
            double h = (b - a) / n;

            for (int i = 0; i < n; i += 2)
            {
                if (i + 2 > n)
                    break;

                double x0 = a + i * h;
                double x1 = x0 + h;
                double x2 = x0 + 2 * h;

                try
                {
                    double y0 = CalculateFunction(x0, function);
                    double y1 = CalculateFunction(x1, function);
                    double y2 = CalculateFunction(x2, function);

                    if (!double.IsInfinity(y0) && !double.IsNaN(y0) &&
                        !double.IsInfinity(y1) && !double.IsNaN(y1) &&
                        !double.IsInfinity(y2) && !double.IsNaN(y2))
                    {
                        DrawParabolaArea(function, x0, x1, x2, padding, plotWidth, plotHeight,
                                         a, b, minY, maxY, brush);
                    }
                }
                catch
                {
                }
            }
        }

        private void DrawParabolaArea(string function, double x0, double x1, double x2,
            double padding, double plotWidth, double plotHeight, double a, double b,
            double minY, double maxY, Brush brush)
        {
            var polygon = new System.Windows.Shapes.Polygon
            {
                Fill = brush,
                Stroke = Brushes.Transparent
            };

            int parabolaPoints = 30;
            for (int j = 0; j <= parabolaPoints; j++)
            {
                double t = j / (double)parabolaPoints;
                double x = x0 + (x2 - x0) * t;

                try
                {
                    double y = CalculateFunction(x, function);
                    if (!double.IsInfinity(y) && !double.IsNaN(y))
                    {
                        double xPos = padding + (x - a) / (b - a) * plotWidth;
                        double yPos = padding + plotHeight - (y - minY) / (maxY - minY) * plotHeight;
                        polygon.Points.Add(new Point(xPos, yPos));
                    }
                }
                catch
                {
                }
            }

            double x2Pos = padding + (x2 - a) / (b - a) * plotWidth;
            double x0Pos = padding + (x0 - a) / (b - a) * plotWidth;
            double bottomY = padding + plotHeight;

            polygon.Points.Add(new Point(x2Pos, bottomY));
            polygon.Points.Add(new Point(x0Pos, bottomY));

            if (polygon.Points.Count >= 3)
            {
                PlotCanvas.Children.Add(polygon);
            }
        }

        private void DrawAxis(double padding, double plotWidth, double plotHeight,
            double a, double b, double minY, double maxY)
        {
            var xAxis = new System.Windows.Shapes.Line
            {
                X1 = padding,
                Y1 = padding + plotHeight,
                X2 = padding + plotWidth,
                Y2 = padding + plotHeight,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
            PlotCanvas.Children.Add(xAxis);

            var yAxis = new System.Windows.Shapes.Line
            {
                X1 = padding,
                Y1 = padding,
                X2 = padding,
                Y2 = padding + plotHeight,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
            PlotCanvas.Children.Add(yAxis);

            int xTicks = 10;
            for (int i = 0; i <= xTicks; i++)
            {
                double xValue = a + (b - a) * i / xTicks;
                double xPos = padding + plotWidth * i / xTicks;

                var tick = new System.Windows.Shapes.Line
                {
                    X1 = xPos,
                    Y1 = padding + plotHeight - 5,
                    X2 = xPos,
                    Y2 = padding + plotHeight + 5,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1
                };
                PlotCanvas.Children.Add(tick);

                var label = new TextBlock
                {
                    Text = xValue.ToString("F2"),
                    FontSize = 9,
                    Foreground = Brushes.Black
                };
                Canvas.SetLeft(label, xPos - 10);
                Canvas.SetTop(label, padding + plotHeight + 5);
                PlotCanvas.Children.Add(label);
            }

            int yTicks = 10;
            for (int i = 0; i <= yTicks; i++)
            {
                double yValue = minY + (maxY - minY) * i / yTicks;
                double yPos = padding + plotHeight - plotHeight * i / yTicks;

                var tick = new System.Windows.Shapes.Line
                {
                    X1 = padding - 5,
                    Y1 = yPos,
                    X2 = padding + 5,
                    Y2 = yPos,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1
                };
                PlotCanvas.Children.Add(tick);

                var label = new TextBlock
                {
                    Text = yValue.ToString("F2"),
                    FontSize = 9,
                    Foreground = Brushes.Black
                };
                Canvas.SetLeft(label, padding - 25);
                Canvas.SetTop(label, yPos - 7);
                PlotCanvas.Children.Add(label);
            }
        }

        private void DrawRectangle(double x1, double y1, double x2, double y2, Brush brush)
        {
            var rect = new System.Windows.Shapes.Rectangle
            {
                Width = x2 - x1,
                Height = y2 - y1,
                Fill = brush,
                Stroke = Brushes.Transparent
            };
            Canvas.SetLeft(rect, x1);
            Canvas.SetTop(rect, y1);
            PlotCanvas.Children.Add(rect);
        }

        private void DrawTrapezoid(double x1, double y1, double x2, double y2, double bottomY, Brush brush)
        {
            var polygon = new System.Windows.Shapes.Polygon
            {
                Fill = brush,
                Stroke = Brushes.Transparent
            };
            polygon.Points.Add(new Point(x1, y1));
            polygon.Points.Add(new Point(x2, y2));
            polygon.Points.Add(new Point(x2, bottomY));
            polygon.Points.Add(new Point(x1, bottomY));
            PlotCanvas.Children.Add(polygon);
        }

        private void Log(string message)
        {
            Dispatcher.Invoke(() =>
            {
                LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                LogTextBox.ScrollToEnd();
            });
        }

        private void StopCalculationButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isCalculating)
            {
                _cancellationTokenSource.Cancel();
                StatusText.Text = "Отмена расчета...";
                StatusText.Foreground = Brushes.Orange;
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource.Cancel();

            FunctionTextBox.Text = "pow(x,2)";
            ATextBox.Text = "0";
            BTextBox.Text = "1";
            EpsilonTextBox.Text = "0.001";
            NTextBox.Text = "100";
            AutoNCheckBox.IsChecked = false;

            RectLeftCheckBox.IsChecked = false;
            RectMiddleCheckBox.IsChecked = false;
            RectRightCheckBox.IsChecked = false;
            TrapezoidalCheckBox.IsChecked = false;
            SimpsonCheckBox.IsChecked = false;

            ResultsPanel.Children.Clear();
            PlotCanvas.Children.Clear();
            LogTextBox.Clear();
            HistoryPanel.Children.Clear();
            NoPlotText.Visibility = Visibility.Visible;

            StatusText.Text = "Готов к работе";
            StatusText.Foreground = Brushes.Green;
            CalculationProgress.Visibility = Visibility.Collapsed;
            ProgressText.Text = "";

            _isCalculating = false;
            _results.Clear();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource.Cancel();
            Close();
        }

        private void GenerateTestFunction_Click(object sender, RoutedEventArgs e)
        {
            var testFunctions = new[]
            {
                "pow(x,2)",
                "sin(x)",
                "cos(x)",
                "exp(x)",
                "x*sin(x)",
                "1/(1+pow(x,2))",
                "sqrt(1+pow(x,2))",
                "log(1+x)",
                "pow(x,3) - 2*x + 1",
                "sin(x)*cos(x)"
            };

            Random rand = new Random();
            FunctionTextBox.Text = testFunctions[rand.Next(testFunctions.Length)];
            ATextBox.Text = "0";
            BTextBox.Text = rand.Next(1, 5).ToString();
            EpsilonTextBox.Text = "0.0001";
            NTextBox.Text = "100";

            RectLeftCheckBox.IsChecked = true;
            RectMiddleCheckBox.IsChecked = true;
            RectRightCheckBox.IsChecked = true;
            TrapezoidalCheckBox.IsChecked = true;
            SimpsonCheckBox.IsChecked = true;
        }

        private void AutoNCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            NTextBox.IsEnabled = false;
            NTextBox.Background = Brushes.LightGray;
        }

        private void AutoNCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            NTextBox.IsEnabled = true;
            NTextBox.Background = Brushes.White;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _cancellationTokenSource.Cancel();
        }

        private string GetMethodDisplayName(string method)
        {
            switch (method)
            {
                case "RectLeft":
                    return "Прямоугольники (левый)";
                case "RectMiddle":
                    return "Прямоугольники (средний)";
                case "RectRight":
                    return "Прямоугольники (правый)";
                case "Trapezoidal":
                    return "Метод трапеций";
                case "Simpson":
                    return "Метод Симпсона";
                default:
                    return method;
            }
        }
    }

    internal class IterationHistory
    {
        public int Iteration { get; set; }
        public int N { get; set; }
        public double Result { get; set; }
    }

    internal class CalculationResult
    {
        public string MethodName { get; set; }
        public double Result { get; set; }
        public TimeSpan Time { get; set; }
        public int Iterations { get; set; }
        public int FinalN { get; set; }
        public List<IterationHistory> History { get; set; } = new List<IterationHistory>();
        public string Error { get; set; }
    }
}

