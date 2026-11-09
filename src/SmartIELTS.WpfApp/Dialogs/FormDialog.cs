using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SmartIELTS.Application.DTOs;
using SmartIELTS.Domain.Enums;
using SmartIELTS.WpfApp.Services;

namespace SmartIELTS.WpfApp.Dialogs;

/// <summary>
/// Hộp thoại nhập liệu dùng chung cho màn quản trị đề thi (không cần MVVM binding phức tạp).
/// </summary>
public class FormDialog : Window
{
    private readonly Dictionary<string, Func<string>> _getters = new();

    public FormDialog(string title, double height = 460)
    {
        Title = title;
        Width = 560;
        Height = height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
        Owner = System.Windows.Application.Current.MainWindow;

        Resources.Add("DlgText", new SolidColorBrush(Color.FromRgb(51, 65, 85)));
        Resources.Add("DlgMuted", new SolidColorBrush(Color.FromRgb(107, 122, 144)));
        Resources.Add("DlgPrimary", new SolidColorBrush(Color.FromRgb(15, 118, 110)));
        Resources.Add("DlgBorder", new SolidColorBrush(Color.FromRgb(221, 227, 234)));
        Resources.Add("DlgSubtle", new SolidColorBrush(Color.FromRgb(238, 242, 247)));

        Resources.Add(typeof(TextBox), BuildInputStyle());
        Resources.Add(typeof(ComboBox), BuildComboStyle());

        var root = new DockPanel { Margin = new Thickness(20) };
        Content = root;

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);

        var ok = new Button
        {
            Content = "Lưu",
            Width = 110,
            Height = 34,
            Margin = new Thickness(0, 14, 8, 0),
            Background = new SolidColorBrush(Color.FromRgb(15, 118, 110)),
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0)
        };

        var cancel = new Button
        {
            Content = "Hủy",
            Width = 90,
            Height = 34,
            Margin = new Thickness(0, 14, 0, 0),
            Background = new SolidColorBrush(Color.FromRgb(238, 242, 247)),
            Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
            BorderThickness = new Thickness(0)
        };

        ok.Click += (_, _) => { DialogResult = true; Close(); };
        cancel.Click += (_, _) => { DialogResult = false; Close(); };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        _fields = new StackPanel();
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _fields };
        root.Children.Add(scroll);
    }

    private static Style BuildInputStyle()
    {
        var style = new Style(typeof(TextBox));
        style.Setters.Add(new Setter(TextBox.BackgroundProperty, Brushes.White));
        style.Setters.Add(new Setter(TextBox.ForegroundProperty, new SolidColorBrush(Color.FromRgb(51, 65, 85))));
        style.Setters.Add(new Setter(TextBox.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(221, 227, 234))));
        style.Setters.Add(new Setter(TextBox.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(8, 5, 8, 5)));
        style.Setters.Add(new Setter(TextBox.FontSizeProperty, 13d));
        return style;
    }

    private static Style BuildComboStyle()
    {
        var style = new Style(typeof(ComboBox));
        style.Setters.Add(new Setter(ComboBox.BackgroundProperty, Brushes.White));
        style.Setters.Add(new Setter(ComboBox.ForegroundProperty, new SolidColorBrush(Color.FromRgb(51, 65, 85))));
        style.Setters.Add(new Setter(ComboBox.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(221, 227, 234))));
        style.Setters.Add(new Setter(ComboBox.FontSizeProperty, 13d));
        style.Setters.Add(new Setter(ComboBox.PaddingProperty, new Thickness(8, 4, 8, 4)));
        return style;
    }

    private readonly StackPanel _fields;

    public void AddText(string key, string label, string initial = "")
    {
        AddLabel(label);
        var box = new TextBox
        {
            Text = initial,
            Height = 32,
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 12)
        };
        _fields.Children.Add(box);
        _getters[key] = () => box.Text.Trim();
    }

    public void AddMultiline(string key, string label, string initial = "", double height = 90)
    {
        AddLabel(label);
        var box = new TextBox
        {
            Text = initial,
            Height = height,
            FontSize = 13,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 0, 0, 12)
        };
        _fields.Children.Add(box);
        _getters[key] = () => box.Text.Trim();
    }

    /// <summary>ComboBox hiển thị enum; trả về tên enum đã chọn.</summary>
    public void AddEnum<TEnum>(string key, string label, TEnum initial) where TEnum : struct, Enum
    {
        AddLabel(label);
        var combo = new ComboBox { Height = 32, FontSize = 13, Margin = new Thickness(0, 0, 0, 12) };
        var values = Enum.GetValues<TEnum>();
        foreach (var value in values) combo.Items.Add(value);
        combo.SelectedItem = values.FirstOrDefault(v => v.Equals(initial));
        if (combo.SelectedItem == null && values.Length > 0) combo.SelectedIndex = 0;

        _fields.Children.Add(combo);
        _getters[key] = () => combo.SelectedItem?.ToString() ?? string.Empty;
    }

    private void AddLabel(string label)
    {
        _fields.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = new SolidColorBrush(Color.FromRgb(107, 122, 144)),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 4)
        });
    }

    public string Get(string key) => _getters.TryGetValue(key, out var getter) ? getter() : string.Empty;

    public int GetInt(string key, int fallback = 0)
        => int.TryParse(Get(key), out int value) ? value : fallback;

    public double GetDouble(string key, double fallback = 0)
        => double.TryParse(Get(key), out double value) ? value : fallback;

    public TEnum GetEnum<TEnum>(string key, TEnum fallback) where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(Get(key), out var value) ? value : fallback;
}
