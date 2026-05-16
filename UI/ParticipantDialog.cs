namespace Koli.UI;

/// <summary>
/// Dialog for pre-registering meeting participants before starting a meeting.
/// </summary>
internal sealed class ParticipantDialog : Form
{
    private readonly ListBox _participantList;
    private readonly TextBox _nameInput;
    private readonly Button _addButton;
    private readonly Button _removeButton;
    private readonly Button _okButton;
    private readonly Button _cancelButton;

    public List<string> Participants { get; } = new();

    public ParticipantDialog()
    {
        Text = "Meeting Participants";
        Size = new Size(400, 420);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = FluentColors.Background;
        ForeColor = FluentColors.TextPrimary;

        var headerLabel = new Label
        {
            Text = "Add participant names (optional — you can rename speakers later):",
            Location = new Point(20, 15),
            Size = new Size(350, 40),
            Font = FluentFonts.Body,
            ForeColor = FluentColors.TextSecondary
        };

        _nameInput = new TextBox
        {
            Location = new Point(20, 60),
            Size = new Size(250, 30),
            Font = FluentFonts.Body,
            BackColor = FluentColors.Surface,
            ForeColor = FluentColors.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle
        };
        _nameInput.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                AddParticipant();
            }
        };

        _addButton = new Button
        {
            Text = "Add",
            Location = new Point(280, 58),
            Size = new Size(80, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = FluentColors.AccentPrimary,
            ForeColor = FluentColors.TextPrimary,
            Font = FluentFonts.ButtonText
        };
        _addButton.FlatAppearance.BorderSize = 0;
        _addButton.Click += (s, e) => AddParticipant();

        _participantList = new ListBox
        {
            Location = new Point(20, 100),
            Size = new Size(250, 220),
            Font = FluentFonts.Body,
            BackColor = FluentColors.Surface,
            ForeColor = FluentColors.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle
        };

        _removeButton = new Button
        {
            Text = "Remove",
            Location = new Point(280, 100),
            Size = new Size(80, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = FluentColors.SurfaceHover,
            ForeColor = FluentColors.TextPrimary,
            Font = FluentFonts.ButtonText
        };
        _removeButton.FlatAppearance.BorderSize = 0;
        _removeButton.Click += (s, e) =>
        {
            if (_participantList.SelectedIndex >= 0)
            {
                _participantList.Items.RemoveAt(_participantList.SelectedIndex);
            }
        };

        _okButton = new Button
        {
            Text = "Start Meeting",
            Location = new Point(160, 340),
            Size = new Size(110, 35),
            FlatStyle = FlatStyle.Flat,
            BackColor = FluentColors.AccentPrimary,
            ForeColor = FluentColors.TextPrimary,
            Font = FluentFonts.ButtonText,
            DialogResult = DialogResult.OK
        };
        _okButton.FlatAppearance.BorderSize = 0;

        _cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(280, 340),
            Size = new Size(80, 35),
            FlatStyle = FlatStyle.Flat,
            BackColor = FluentColors.SurfaceHover,
            ForeColor = FluentColors.TextSecondary,
            Font = FluentFonts.ButtonText,
            DialogResult = DialogResult.Cancel
        };
        _cancelButton.FlatAppearance.BorderSize = 0;

        AcceptButton = _okButton;
        CancelButton = _cancelButton;

        _okButton.Click += (s, e) =>
        {
            foreach (var item in _participantList.Items)
                Participants.Add(item.ToString()!);
        };

        Controls.AddRange(new Control[] { headerLabel, _nameInput, _addButton, _participantList, _removeButton, _okButton, _cancelButton });
    }

    private void AddParticipant()
    {
        var name = _nameInput.Text.Trim();
        if (!string.IsNullOrEmpty(name) && !_participantList.Items.Contains(name))
        {
            _participantList.Items.Add(name);
            _nameInput.Clear();
            _nameInput.Focus();
        }
    }
}
