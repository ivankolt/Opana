using Npgsql;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace UchPR
{
    public class CutPiece
    {
        public double Length { get; set; }
        public double Width { get; set; }
    }

    public partial class CuttingWindow : Window
    {
        private List<OrderItem> orderItems;
        private ObservableCollection<CutPiece> currentCuts = new ObservableCollection<CutPiece>();
        private int orderNumber;
        private DateTime orderDate;
        private DataBase database;
        private bool cutsSaved = false;

        public CuttingWindow(int orderNumber, DateTime orderDate, List<OrderItem> items)
        {
            InitializeComponent();
            this.orderNumber = orderNumber;
            this.orderDate = orderDate;
            orderItems = items;
            lbProducts.ItemsSource = orderItems;
            database = new DataBase(); // добавьте эту строку!
        }


        private void SaveCutsToDb()
        {
            if (lbProducts.SelectedItem is OrderItem item)
            {
                string article = item.ProductArticle;

                // Удаляем старые отрезки
                string deleteQuery = @"DELETE FROM productcuts WHERE order_number=@n AND order_date=@d AND product_article=@a";
                database.ExecuteNonQuery(deleteQuery, new[] {
            new NpgsqlParameter("@n", orderNumber),
            new NpgsqlParameter("@d", orderDate),
            new NpgsqlParameter("@a", article)
        });

                // Добавляем новые
                int idx = 0;
                foreach (var cut in currentCuts)
                {
                    string insertQuery = @"INSERT INTO productcuts(order_number, order_date, product_article, cut_index, length, width)
                                   VALUES (@n, @d, @a, @i, @l, @w)";
                    database.ExecuteNonQuery(insertQuery, new[] {
                new NpgsqlParameter("@n", orderNumber),
                new NpgsqlParameter("@d", orderDate),
                new NpgsqlParameter("@a", article),
                new NpgsqlParameter("@i", idx++),
                new NpgsqlParameter("@l", cut.Length),
                new NpgsqlParameter("@w", cut.Width)
            });
                }
            }
        }

        private void LoadCutsFromDb()
        {
            if (lbProducts.SelectedItem is OrderItem item)
            {
                string article = item.ProductArticle;
                string selectQuery = @"SELECT length, width FROM productcuts
                               WHERE order_number=@n AND order_date=@d AND product_article=@a
                               ORDER BY cut_index";
                var dt = database.GetData(selectQuery, new[] {
            new NpgsqlParameter("@n", orderNumber),
            new NpgsqlParameter("@d", orderDate),
            new NpgsqlParameter("@a", article)
        });

                currentCuts.Clear();
                foreach (DataRow row in dt.Rows)
                {
                    currentCuts.Add(new CutPiece
                    {
                        Length = Convert.ToDouble(row["length"]),
                        Width = Convert.ToDouble(row["width"])
                    });
                }
            }
        }


        private void lbProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            currentCuts.Clear();
            LoadCutsFromDb();
            canvasVisual.Children.Clear();
            BtnVisualize_Click(null, null);
        }
        private void BtnSaveCuts_Click(object sender, RoutedEventArgs e)
        {
            SaveCutsToDb();
            MessageBox.Show("Обрезки для изделия успешно сохранены!", "Сохранено", MessageBoxButton.OK, MessageBoxImage.Information);
            btnSaveCuts.IsEnabled = false; // где btnSaveCuts — x:Name вашей кнопки "Сохранить обрезки"
            cutsSaved = true;
        }

        private void BtnAddCut_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(txtCutLength.Text, out double len) && double.TryParse(txtCutWidth.Text, out double wid))
            {
                currentCuts.Add(new CutPiece { Length = len, Width = wid });
                BtnVisualize_Click(null, null);
            }
            else
            {
                MessageBox.Show("Введите корректные размеры отрезка.");
            }
        }
        private void BtnVisualize_Click(object sender, RoutedEventArgs e)
        {
            canvasVisual.Children.Clear();
            if (lbProducts.SelectedItem is OrderItem item)
            {
                double scale = 2.0;
                double prodLen = (double)item.Length;
                double prodWid = (double)item.Width;
                int count = item.Quantity;

                // Для каждого экземпляра изделия храним размещённые отрезки
                var placedCuts = new List<List<(double x, double y, double w, double h, CutPiece cut, bool rotated)>>();
                for (int i = 0; i < count; i++)
                    placedCuts.Add(new List<(double, double, double, double, CutPiece, bool)>());

                // Координаты и состояния для каждого изделия
                double[] shelfX = new double[count];
                double[] shelfY = new double[count];
                double[] shelfHeight = new double[count];

                for (int i = 0; i < count; i++)
                {
                    shelfX[i] = 0;
                    shelfY[i] = 0;
                    shelfHeight[i] = 0;
                }

                int currItem = 0;
                foreach (var cut in currentCuts)
                {
                    bool placed = false;
                    // Пытаемся разместить на текущем изделии, если не помещается — переходим к следующему
                    for (int n = currItem; n < count; n++)
                    {
                        // 1. Попробуем без поворота
                        double cutW = cut.Length * scale;
                        double cutH = cut.Width * scale;

                        if (shelfX[n] + cutW > prodLen * scale)
                        {
                            shelfY[n] += shelfHeight[n];
                            shelfX[n] = 0;
                            shelfHeight[n] = 0;
                        }
                        if (shelfY[n] + cutH <= prodWid * scale)
                        {
                            // Помещается без поворота
                            placedCuts[n].Add((shelfX[n], shelfY[n], cutW, cutH, cut, false));
                            shelfX[n] += cutW;
                            shelfHeight[n] = Math.Max(shelfHeight[n], cutH);
                            currItem = n;
                            placed = true;
                            break;
                        }

                        // 2. Попробуем с поворотом (меняем местами длину и ширину)
                        cutW = cut.Width * scale;
                        cutH = cut.Length * scale;

                        if (shelfX[n] + cutW > prodLen * scale)
                        {
                            shelfY[n] += shelfHeight[n];
                            shelfX[n] = 0;
                            shelfHeight[n] = 0;
                        }
                        if (shelfY[n] + cutH <= prodWid * scale)
                        {
                            // Помещается с поворотом
                            placedCuts[n].Add((shelfX[n], shelfY[n], cutW, cutH, cut, true));
                            shelfX[n] += cutW;
                            shelfHeight[n] = Math.Max(shelfHeight[n], cutH);
                            currItem = n;
                            placed = true;
                            break;
                        }
                        // Если не помещается ни так, ни так — переходим к следующему изделию
                    }
                    if (!placed)
                    {
                        var warn = new TextBlock
                        {
                            Text = $"Обрезок {cut.Length}x{cut.Width} не помещается!",
                            Foreground = Brushes.Red,
                            FontWeight = FontWeights.Bold,
                            FontSize = 16
                        };
                        Canvas.SetLeft(warn, 10);
                        Canvas.SetTop(warn, prodWid * scale * count + 10);
                        canvasVisual.Children.Add(warn);
                    }
                }

                // Теперь рисуем все изделия и их размещённые отрезки
                double offsetY = 30;
                for (int n = 0; n < count; n++)
                {
                    // Рисуем изделие
                    var borderRect = new Rectangle
                    {
                        Width = prodLen * scale,
                        Height = prodWid * scale,
                        Stroke = Brushes.Black,
                        StrokeThickness = 3,
                        Fill = Brushes.Transparent
                    };
                    Canvas.SetLeft(borderRect, 0);
                    Canvas.SetTop(borderRect, offsetY);
                    canvasVisual.Children.Add(borderRect);

                    var label = new TextBlock
                    {
                        Text = $"{item.ProductName} #{n + 1} ({prodLen}x{prodWid} см)",
                        FontWeight = FontWeights.Bold,
                        FontSize = 14
                    };
                    Canvas.SetLeft(label, 5);
                    Canvas.SetTop(label, offsetY - 25);
                    canvasVisual.Children.Add(label);

                    // Рисуем размещённые на этом изделии отрезки
                    foreach (var (x, y, w, h, cut, rotated) in placedCuts[n])
                    {
                        var cutRect = new Rectangle
                        {
                            Width = w,
                            Height = h,
                            Stroke = Brushes.Blue,
                            StrokeThickness = 1,
                            Fill = rotated ? Brushes.Orange : Brushes.LightBlue,
                            Opacity = 0.7
                        };
                        Canvas.SetLeft(cutRect, x);
                        Canvas.SetTop(cutRect, offsetY + y);
                        canvasVisual.Children.Add(cutRect);

                        var cutLabel = new TextBlock
                        {
                            Text = rotated
                                ? $"{cut.Length}x{cut.Width} (повёрнут)"
                                : $"{cut.Length}x{cut.Width}",
                            FontSize = 12
                        };
                        Canvas.SetLeft(cutLabel, x + 3);
                        Canvas.SetTop(cutLabel, offsetY + y + 3);
                        canvasVisual.Children.Add(cutLabel);
                    }

                    offsetY += prodWid * scale + 40; // сместить вниз для следующего изделия
                }
            }
        }

    }
}
