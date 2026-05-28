using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace Koli.WinUI.Dialogs;

public sealed class ParticipantDialog : ContentDialog
{
    private readonly TextBox _nameInput;
    private readonly ListView _list;

    public List<string> Participants { get; } = new();

    public ParticipantDialog()
    {
        Title = "Meeting participants";
        PrimaryButtonText = "Start meeting";
        CloseButtonText = "Cancel";
        DefaultButton = ContentDialogButton.Primary;

        var panel = new StackPanel { Spacing = 12, MinWidth = 380 };
        panel.Children.Add(new TextBlock
        {
            Text = "Add participant names (optional — you can rename speakers later).",
            TextWrapping = TextWrapping.WrapWholeWords,
            Foreground = Application.Current.Resources["TextSecondaryBrush"] as Brush
        });

        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _nameInput = new TextBox { PlaceholderText = "Add a name…" };
        Grid.SetColumn(_nameInput, 0);
        row.Children.Add(_nameInput);

        var addBtn = new Button
        {
            Style = Application.Current.Resources["PrimaryButtonStyle"] as Style,
            Padding = new Thickness(14, 6, 14, 6),
            VerticalAlignment = VerticalAlignment.Bottom
        };
        var addContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        addContent.Children.Add(new FontIcon { Glyph = "", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) });
        addContent.Children.Add(new TextBlock { Text = "Add" });
        addBtn.Content = addContent;
        addBtn.Click += (_, _) => AddName();
        Grid.SetColumn(addBtn, 1);
        row.Children.Add(addBtn);

        _nameInput.KeyDown += (_, e) =>
        {
            if (e.Key == global::Windows.System.VirtualKey.Enter)
            {
                AddName();
                e.Handled = true;
            }
        };
        panel.Children.Add(row);

        _list = new ListView
        {
            MinHeight = 200,
            SelectionMode = ListViewSelectionMode.Single,
            Background = Application.Current.Resources["CardSubtleBackgroundBrush"] as Brush,
            BorderBrush = Application.Current.Resources["BorderBrush"] as Brush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(4)
        };
        panel.Children.Add(_list);

        var removeBtn = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var removeContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        removeContent.Children.Add(new FontIcon
        {
            Glyph = "",
            FontSize = 12,
            Foreground = Application.Current.Resources["ErrorBrush"] as Brush
        });
        removeContent.Children.Add(new TextBlock { Text = "Remove selected" });
        removeBtn.Content = removeContent;
        removeBtn.Click += (_, _) =>
        {
            if (_list.SelectedItem is string selected)
                _list.Items.Remove(selected);
        };
        panel.Children.Add(removeBtn);

        Content = panel;
        PrimaryButtonClick += (_, _) =>
        {
            Participants.Clear();
            foreach (var item in _list.Items)
                if (item is string s)
                    Participants.Add(s);
        };
    }

    private void AddName()
    {
        var name = _nameInput.Text.Trim();
        if (string.IsNullOrEmpty(name))
            return;
        if (!_list.Items.Contains(name))
            _list.Items.Add(name);
        _nameInput.Text = "";
    }
}
