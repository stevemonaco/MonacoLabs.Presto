using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Builder;
using PrestoSample.Pages;
using PrestoSample.Showcase;

namespace PrestoSample.Tests;

/// <summary>The code a page prints compiles against the real API once its free identifiers are declared.</summary>
public sealed class SnippetCompileTests
{
    [AvaloniaFact]
    public void AnimationBasicsPrintsACompilingCall() => AssertCompiles(
        "Button LikeButton;",
        Printed(new AnimationBasicsPage(), panel => panel.Name == "Code") + ";");

    [AvaloniaFact]
    public void TimelinePrintsACompilingChain() => AssertCompiles(
        """
        Button SyncedButton, AbsoluteButton, LabelButton; ToggleSwitch AppendToggle; Slider RelativeSlider;
        TextBox PreviousEndTextBox; CheckBox PreviousStartCheck; TextBlock TimerChildText, CallText;
        Timeline? _timeline; int _callHits; void UpdateReadouts() { }
        """,
        Printed(new TimelinePage(), panel => panel.Name == "Code"));

    [AvaloniaFact]
    public void BuilderBasicsPrintsACompilingBuilder() => AssertCompiles(
        "Button LikeButton;",
        Printed(new BuilderBasicsPage(), panel => panel.Name == "CodeText") + ";");

    [AvaloniaFact]
    public void CarouselPrintsACompilingParallaxRegion() => AssertCompiles(
        """
        const double BackgroundDepth = -.5, ArtDepth = -.2, TitleLead = .35;
        List<(Control Background, Control Art, Control Title)> _layers = []; Draggable _drag; double _slideWidth;
        """,
        Printed(new Showcase.Pages.CarouselPage(), panel => panel.Header == "How it's built"));

    [AvaloniaFact]
    public void DraggablePrintsACompilingSpring() => AssertCompiles(
        """
        Border SpringCard; Canvas Stage; int _drags, _updates;
        Func<string, Action<Draggable>> Record; Action<Draggable> ShowState;
        """,
        Printed(new DraggablePage(), panel => panel.Name == "SpringCode") + ";");

    private static string Printed(Control page, Func<CodePanel, bool> panel)
    {
        var window = SceneHarness.Show(page, 1280, 900, frames: 5);
        var code = page.GetLogicalDescendants().OfType<CodePanel>().Single(panel).Code!;
        SceneHarness.Close(window);
        return code;
    }

    private static void AssertCompiles(string members, string body)
    {
        var source = $$"""
            using System;
            using System.Collections.Generic;
            using System.Globalization;
            using System.Linq;
            using Avalonia.Controls;
            using Monaco.Presto;
            using Monaco.Presto.AvaloniaBridge;
            using Monaco.Presto.AvaloniaBridge.Draggables;
            using PrestoSample.Showcase;

            sealed class Scratch
            {
            {{members}}

            void Run()
            {
            {{body}}
            }
            }
            """;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Concat(new[] { typeof(object), typeof(Presto), typeof(PrestoAvalonia), typeof(AvaloniaObject), typeof(Control), typeof(CodePanel) }
                .Select(type => type.Assembly.Location))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("Scratch", [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var errors = compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, $"{string.Join(Environment.NewLine, errors)}{Environment.NewLine}{source}");
    }
}
