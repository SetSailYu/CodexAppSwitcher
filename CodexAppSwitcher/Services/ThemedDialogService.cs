using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CodexAppSwitcher.Services;

/// <summary>
/// 深色主题确认弹窗服务。
/// </summary>
public static class ThemedDialogService
{
    /// <summary>
    /// 显示文本输入弹窗。
    /// </summary>
    public static string? Prompt(Window? owner, string title, string message, string initialValue, string primaryText, string secondaryText)
    {
        if (owner is null)
        {
            return null;
        }

        var dialog = new Window
        {
            Owner = owner,
            Title = title,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            Background = Resource<Brush>(owner, "PanelBrush"),
            Foreground = Resource<Brush>(owner, "TextPrimaryBrush"),
            FontFamily = owner.FontFamily,
            ShowInTaskbar = false
        };

        var root = CreateDialogRoot(owner);
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var titleBlock = CreateTitleBlock(owner, title);
        Grid.SetRow(titleBlock, 0);
        layout.Children.Add(titleBlock);

        var messageBlock = new TextBlock
        {
            Text = message,
            Foreground = Resource<Brush>(owner, "TextMutedBrush"),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 22,
            Margin = new Thickness(0, 0, 0, 10)
        };
        Grid.SetRow(messageBlock, 1);
        layout.Children.Add(messageBlock);

        var input = new TextBox
        {
            Text = initialValue,
            Height = 34,
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 18),
            Background = Resource<Brush>(owner, "ControlBrush"),
            Foreground = Resource<Brush>(owner, "TextPrimaryBrush"),
            BorderBrush = Resource<Brush>(owner, "BorderBrush"),
            SelectionBrush = Resource<Brush>(owner, "AccentBrush")
        };
        input.SelectAll();
        Grid.SetRow(input, 2);
        layout.Children.Add(input);

        var buttons = CreateButtonPanel();
        var secondaryButton = CreateSecondaryButton(secondaryText);
        secondaryButton.Click += (_, _) =>
        {
            dialog.DialogResult = false;
            dialog.Close();
        };
        buttons.Children.Add(secondaryButton);

        var primaryButton = CreatePrimaryButton(owner, primaryText);
        primaryButton.Click += (_, _) =>
        {
            dialog.DialogResult = true;
            dialog.Close();
        };
        buttons.Children.Add(primaryButton);
        Grid.SetRow(buttons, 3);
        layout.Children.Add(buttons);

        root.Child = layout;
        dialog.Content = root;
        dialog.Loaded += (_, _) => input.Focus();
        return dialog.ShowDialog() == true ? input.Text : null;
    }

    /// <summary>
    /// 显示确认弹窗。
    /// </summary>
    public static bool Confirm(Window? owner, string title, string message, string primaryText, string secondaryText)
    {
        if (owner is null)
        {
            return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }

        var dialog = new Window
        {
            Owner = owner,
            Title = title,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            Background = Resource<Brush>(owner, "PanelBrush"),
            Foreground = Resource<Brush>(owner, "TextPrimaryBrush"),
            FontFamily = owner.FontFamily,
            ShowInTaskbar = false
        };

        var root = CreateDialogRoot(owner);

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var titleBlock = CreateTitleBlock(owner, title);
        Grid.SetRow(titleBlock, 0);
        layout.Children.Add(titleBlock);

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 18)
        };
        var badge = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(14),
            Background = Resource<Brush>(owner, "ControlBrush"),
            BorderBrush = Resource<Brush>(owner, "AccentBrush"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 2, 12, 0)
        };
        badge.Child = new TextBlock
        {
            Text = "!",
            Foreground = Resource<Brush>(owner, "AccentBrush"),
            FontSize = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        content.Children.Add(badge);
        content.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = Resource<Brush>(owner, "TextPrimaryBrush"),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Width = 340,
            LineHeight = 22
        });
        Grid.SetRow(content, 1);
        layout.Children.Add(content);

        var buttons = CreateButtonPanel();

        var secondaryButton = CreateSecondaryButton(secondaryText);
        secondaryButton.Click += (_, _) =>
        {
            dialog.DialogResult = false;
            dialog.Close();
        };
        buttons.Children.Add(secondaryButton);

        var primaryButton = CreatePrimaryButton(owner, primaryText);
        primaryButton.Click += (_, _) =>
        {
            dialog.DialogResult = true;
            dialog.Close();
        };
        buttons.Children.Add(primaryButton);
        Grid.SetRow(buttons, 2);
        layout.Children.Add(buttons);

        root.Child = layout;
        dialog.Content = root;
        return dialog.ShowDialog() == true;
    }

    private static Border CreateDialogRoot(FrameworkElement owner) => new()
    {
        Background = Resource<Brush>(owner, "PanelBrush"),
        BorderBrush = Resource<Brush>(owner, "BorderBrush"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(18)
    };

    private static TextBlock CreateTitleBlock(FrameworkElement owner, string title) => new()
    {
        Text = title,
        FontSize = 16,
        Foreground = Resource<Brush>(owner, "TextPrimaryBrush"),
        Margin = new Thickness(0, 0, 0, 12)
    };

    private static StackPanel CreateButtonPanel() => new()
    {
        Orientation = Orientation.Horizontal,
        HorizontalAlignment = HorizontalAlignment.Right
    };

    private static Button CreateSecondaryButton(string text) => new()
    {
        Content = text,
        Width = 96,
        Height = 34,
        Margin = new Thickness(0, 0, 8, 0)
    };

    private static Button CreatePrimaryButton(FrameworkElement owner, string text) => new()
    {
        Content = text,
        Width = 116,
        Height = 34,
        Style = Resource<Style>(owner, "PrimaryActionButtonStyle")
    };

    private static T Resource<T>(FrameworkElement owner, string key) where T : class
    {
        return (T)owner.FindResource(key);
    }
}
