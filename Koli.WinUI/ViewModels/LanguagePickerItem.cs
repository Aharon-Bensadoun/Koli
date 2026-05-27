namespace Koli.WinUI.ViewModels;

public sealed class LanguagePickerItem
{
    public required string Label { get; init; }
    public required string Code { get; init; }

    public override string ToString() => Label;
}

public sealed class OutputLanguageModeItem
{
    public required string Label { get; init; }
    public required string Value { get; init; }

    public override string ToString() => Label;
}
