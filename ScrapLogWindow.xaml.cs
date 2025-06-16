// File: ScrapLogWindow.xaml.cs
using System;
using System.Linq;
using System.Windows;

namespace UchPR
{
    public partial class ScrapLogWindow : Window
    {
        private readonly DataBase db = new DataBase();

        public ScrapLogWindow()
        {
            InitializeComponent();
            LoadScrapLog();

            // Устанавливаем даты по умолчанию (текущий месяц)
            dpFrom.SelectedDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            dpTo.SelectedDate = DateTime.Now;
        }

        private void LoadScrapLog()
        {
            var scrapLog = db.GetScrapLog(dpFrom.SelectedDate, dpTo.SelectedDate);
            dgScrapLog.ItemsSource = scrapLog;

            // Подсчитываем общую стоимость
            decimal totalCost = scrapLog.Sum(item => item.CostScrap);
            txtTotalScrapCost.Text = totalCost.ToString("C");
        }

        private void btnFilter_Click(object sender, RoutedEventArgs e)
        {
            LoadScrapLog();
        }
    }
}
