using Npgsql;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace UchPR
{
   

    public partial class ViewCutsWindow : Window
    {
        private List<OrderItem> orderItems;
        private ObservableCollection<CutPiece> currentCuts = new ObservableCollection<CutPiece>();
        private int orderNumber;
        private DateTime orderDate;
        private DataBase database;

        public ViewCutsWindow(int orderNumber, DateTime orderDate, List<OrderItem> items)
        {
            InitializeComponent();
            this.orderNumber = orderNumber;
            this.orderDate = orderDate;
            orderItems = items;
            lbProducts.ItemsSource = orderItems;
            database = new DataBase();
        }

        private void lbProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            currentCuts.Clear();
            LoadCutsFromDb();
            canvasVisual.Children.Clear();
            VisualizeCuts();
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

        private void VisualizeCuts()
        {
            if (lbProducts.SelectedItem is OrderItem item)
            {
                double scale = 2.0;
                double prodLen = (double)item.Length;
                double prodWid = (double)item.Width;

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
                Canvas.SetTop(borderRect, 0);
                canvasVisual.Children.Add(borderRect);

                var label = new TextBlock
                {
                    Text = $"{item.ProductName} ({prodLen}x{prodWid} см)",
                    FontWeight = FontWeights.Bold,
                    FontSize = 14
                };
                Canvas.SetLeft(label, 5);
                Canvas.SetTop(label, -25);
                canvasVisual.Children.Add(label);

                // Плотная упаковка без отступов
                double shelfY = 0, shelfHeight = 0, shelfX = 0;
                bool hasSpace = true;

                foreach (var cut in currentCuts)
                {
                    double cutW = cut.Length * scale;
                    double cutH = cut.Width * scale;

                    if (shelfX + cutW > prodLen * scale)
                    {
                        shelfY += shelfHeight;
                        shelfX = 0;
                        shelfHeight = 0;
                    }
                    if (shelfY + cutH > prodWid * scale)
                    {
                        hasSpace = false;
                        break;
                    }

                    var cutRect = new Rectangle
                    {
                        Width = cutW,
                        Height = cutH,
                        Stroke = Brushes.Blue,
                        StrokeThickness = 1,
                        Fill = Brushes.LightBlue,
                        Opacity = 0.7
                    };
                    Canvas.SetLeft(cutRect, shelfX);
                    Canvas.SetTop(cutRect, shelfY);
                    canvasVisual.Children.Add(cutRect);

                    var cutLabel = new TextBlock
                    {
                        Text = $"{cut.Length} x {cut.Width}",
                        FontSize = 12
                    };
                    Canvas.SetLeft(cutLabel, shelfX + 3);
                    Canvas.SetTop(cutLabel, shelfY + 3);
                    canvasVisual.Children.Add(cutLabel);

                    shelfX += cutW;
                    shelfHeight = Math.Max(shelfHeight, cutH);
                }

                if (!hasSpace)
                {
                    var warn = new TextBlock
                    {
                        Text = "Больше нет места!",
                        Foreground = Brushes.Red,
                        FontWeight = FontWeights.Bold,
                        FontSize = 16
                    };
                    Canvas.SetLeft(warn, 10);
                    Canvas.SetTop(warn, prodWid * scale + 10);
                    canvasVisual.Children.Add(warn);
                }
            }
        }
    }
}
