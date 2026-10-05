using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace PrestoSample.Showcase;

/// <summary>A <see cref="CodePanel"/> with a C#/XAML switch; with only one side set it shows that side and no switch.</summary>
public sealed class CodeTabs : TemplatedControl
{
    public static readonly StyledProperty<string?> CSharpProperty = AvaloniaProperty.Register<CodeTabs, string?>(nameof(CSharp));

    public static readonly StyledProperty<string?> XamlProperty = AvaloniaProperty.Register<CodeTabs, string?>(nameof(Xaml));

    public static readonly StyledProperty<string?> HeaderProperty = CodePanel.HeaderProperty.AddOwner<CodeTabs>();

    public static readonly StyledProperty<bool> IsExpandedProperty = CodePanel.IsExpandedProperty.AddOwner<CodeTabs>();

    public static readonly StyledProperty<CodeLanguage> SelectedLanguageProperty =
        AvaloniaProperty.Register<CodeTabs, CodeLanguage>(nameof(SelectedLanguage));

    private Panel? _tabs;
    private ToggleButton? _cSharpTab;
    private ToggleButton? _xamlTab;
    private CodePanel? _panel;

    public string? CSharp
    {
        get => GetValue(CSharpProperty);
        set => SetValue(CSharpProperty, value);
    }

    public string? Xaml
    {
        get => GetValue(XamlProperty);
        set => SetValue(XamlProperty, value);
    }

    /// <summary>A title above the code; with one the code collapses, and the switch shows only while expanded.</summary>
    public string? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public CodeLanguage SelectedLanguage
    {
        get => GetValue(SelectedLanguageProperty);
        set => SetValue(SelectedLanguageProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _tabs = e.NameScope.Find<Panel>("PART_Tabs");
        _cSharpTab = e.NameScope.Find<ToggleButton>("PART_CSharpTab");
        _xamlTab = e.NameScope.Find<ToggleButton>("PART_XamlTab");
        _panel = e.NameScope.Find<CodePanel>("PART_Panel");
        _cSharpTab?.Click += (_, _) => Select(CodeLanguage.CSharp);
        _xamlTab?.Click += (_, _) => Select(CodeLanguage.Xaml);
        Show();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CSharpProperty || change.Property == XamlProperty || change.Property == SelectedLanguageProperty || change.Property == IsExpandedProperty) Show();
    }

    private void Select(CodeLanguage language)
    {
        SelectedLanguage = language;
        Show();
    }

    private void Show()
    {
        if (_panel is null) return;
        var both = !string.IsNullOrEmpty(CSharp) && !string.IsNullOrEmpty(Xaml);
        var language = both ? SelectedLanguage : string.IsNullOrEmpty(CSharp) ? CodeLanguage.Xaml : CodeLanguage.CSharp;
        _panel.Language = language;
        _panel.Code = language == CodeLanguage.Xaml ? Xaml : CSharp;
        _tabs?.IsVisible = both && IsExpanded;
        _cSharpTab?.IsChecked = language == CodeLanguage.CSharp;
        _xamlTab?.IsChecked = language == CodeLanguage.Xaml;
    }
}
