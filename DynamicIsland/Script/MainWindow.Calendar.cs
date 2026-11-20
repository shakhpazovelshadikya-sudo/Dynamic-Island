using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DynamicIsland;

/// <summary>Вкладка «Календарь».</summary>
public partial class MainWindow
{
    private DateTime _calMonth = DateTime.Today;
    private DateTime _calBuiltFor = DateTime.MinValue;

    // ---------- Календарь ----------
    private void CalPrev_Click(object sender, RoutedEventArgs e) { _calMonth = _calMonth.AddMonths(-1); BuildCalendar(); }
    private void CalNext_Click(object sender, RoutedEventArgs e) { _calMonth = _calMonth.AddMonths(1); BuildCalendar(); }
    private void CalMonth_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) { _calMonth = DateTime.Today; BuildCalendar(); }

    private void BuildCalendar()
    {
        var today = DateTime.Today;
        _calBuiltFor = today;

        CalDay.Text = today.Day.ToString();
        CalWeekday.Text = Cap(today.ToString("dddd", Ru));
        CalWeek.Text = Loc.F("неделя {0}", ISOWeek.GetWeekOfYear(today));

        string month = Cap(_calMonth.ToString("MMMM", Ru));
        CalMonth.Text = _calMonth.Year == today.Year ? month : $"{month} {_calMonth.Year}";

        CalGrid.Children.Clear();
        foreach (var h in new[] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" }.Select(Loc.T))
            CalGrid.Children.Add(CalCell(h, "#777777", null, 10));

        var first = new DateTime(_calMonth.Year, _calMonth.Month, 1);
        int offset = ((int)first.DayOfWeek + 6) % 7; // понедельник = 0
        var start = first.AddDays(-offset);

        for (int i = 0; i < 42; i++)
        {
            var d = start.AddDays(i);
            bool inMonth = d.Month == _calMonth.Month;
            bool isToday = d == today;
            bool weekend = d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday;

            string fg = isToday ? "#000000" : !inMonth ? "#444444" : weekend ? "#FF8A8A" : "#FFFFFF";
            CalGrid.Children.Add(CalCell(d.Day.ToString(), fg, isToday ? Brushes.White : null, 11));
        }
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpper(s[0], Ru) + s.Substring(1);

    private static UIElement CalCell(string text, string fg, Brush? bg, double size)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = bg != null ? FontWeights.Bold : FontWeights.Normal,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fg)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        return new Border
        {
            Background = bg,
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(1.5, 0.5, 1.5, 0.5),
            Child = tb
        };
    }
}
