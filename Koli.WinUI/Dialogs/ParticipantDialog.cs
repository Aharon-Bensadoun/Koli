using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Koli.WinUI.Dialogs;

public sealed class ParticipantDialog : ContentDialog
{
    private readonly TextBox _nameInput;
    private readonly ListView _list;

    public List<string> Participants { get; } = new();

    public ParticipantDialog()
    {
        Title = "Meeting Participants";
        PrimaryButtonText = "Start Meeting";
        CloseButtonText = "Cancel";
        DefaultButton = ContentDialogButton.Primary;

        var panel = new StackPanel { Spacing = 12, MinWidth = 360 };
        panel.Children.Add(new TextBlock
        {
            Text = "Add participant names (optional — you can rename speakers later).",
            TextWrapping = TextWrapping.WrapWholeWords
        });

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _nameInput = new TextBox { PlaceholderText = "Name", Width = 240 };
        var addBtn = new Button { Content = "Add" };
        addBtn.Click += (_, _) => AddName();
        _nameInput.KeyDown += (_, e) =>
        {
            if (e.Key == global::Windows.System.VirtualKey.Enter)
            {
                AddName();
                e.Handled = true;
            }
        };
        row.Children.Add(_nameInput);
        row.Children.Add(addBtn);
        panel.Children.Add(row);

        _list = new ListView { MinHeight = 180 };
        panel.Children.Add(_list);

        var removeBtn = new Button { Content = "Remove selected", HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left };
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
