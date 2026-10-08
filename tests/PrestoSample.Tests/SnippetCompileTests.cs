using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Lottie;
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
        List<(Control Background, Control Art, Control Title)> _layers = []; Pager Pager;
        """,
        Printed(new Showcase.Pages.CarouselPage(), panel => panel.Header == "How it's built"));

    [AvaloniaFact]
    public void CompositorThreadModesPrintCompilingCode() => AssertCompiles(
        $"TextBlock UnfitLabel, readout; Border dot; AnimationBuilder builder; AnimationThread thread;\n{Snippets.ThreadBuilders}",
        $"{Snippets.ThreadQuery}\n{Snippets.ThreadModes}");

    [AvaloniaFact]
    public void DraggablePrintsACompilingSpring() => AssertCompiles(
        """
        Border SpringCard; Canvas Stage; int _drags, _updates;
        Func<string, Action<Draggable>> Record; Action<Draggable> ShowState;
        """,
        Printed(new DraggablePage(), panel => panel.Name == "SpringCode") + ";");

    [AvaloniaFact]
    public void SpringLabPrintsACompilingDrop() => AssertCompiles(
        "Border Weight; const double Drop = 120; void DrawCoil(double y) { }",
        Printed(new SpringsPage(), panel => panel.Name == "LabCode"));

    [AvaloniaFact]
    public void SpringRegimesPrintCompilingSpringsAndRace() => AssertCompiles(
        $"Border UnderDot, CriticalDot, OverDot; const double RaceDistance = 300;\n{Snippets.SpringRegimes}",
        Snippets.SpringRace);

    [AvaloniaFact]
    public void SpringVelocityPrintsACompilingFollowerAndRetarget() => AssertCompiles(
        "Border TweenPuck, SpringPuck; MotionValue _targetX, _targetY, _followerX; const double TweenDuration = 600; double x, y;",
        Printed(new SpringsPage(), panel => panel.Name == "VelocityCode"));

    [AvaloniaFact]
    public void SpringThrowPrintsACompilingChaseMoveAndRelease() => AssertCompiles(
        "Border ThrowPuck; Animatable _throw; VelocityTracker _trackX = new(), _trackY = new(); double x, y;",
        Printed(new SpringsPage(), panel => panel.Name == "ThrowCode"));

    [AvaloniaFact]
    public void SpringEaseOrFollowerPrintsCompilingRows() => AssertCompiles(
        "StackPanel EaseRow, FollowerRow; Spring _rowSpring; MotionValue _rowTarget; bool _easeDown; const double RowDrop = 150; Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e;",
        Printed(new SpringsPage(), panel => panel.Name == "EaseFollowerCode"));

    [AvaloniaFact]
    public void SpringPresetsPrintCompilingHandlers() => AssertCompiles(
        """
        Border ToggleThumb, TabIndicator, Badge; StackPanel Tabs; TextBlock BadgeCount; object sender;
        bool _toggled, open; int _unread; double SheetTop, ScrimOpacity;
        const double ToggleTravel = 26, TabWidth = 64, SheetOpen = 50, SheetClosed = 140, ScrimShown = .5;
        """,
        $"{Snippets.SpringPresetToggle}\n{Snippets.SpringPresetTabs}\n{Snippets.SpringPresetBadge}\n{Snippets.SpringPresetSheetOpen}");

    [AvaloniaFact]
    public void SpringChainPrintsCompilingSetupMoveAndSwap() => AssertCompiles(
        """
        Canvas ChainStage; Border[] chain; Spring _chainSpring; MotionValue _pointerX, _pointerY; object sender;
        Avalonia.Input.PointerEventArgs e; static readonly (double Bounce, double Duration)[] ChainSprings = [];
        """,
        $"{{\n{Snippets.SpringChain}\n}}\n{{\n{Snippets.SpringChainMove}\n}}\n{{\n{Snippets.SpringChainSwap}\n}}");

    [AvaloniaFact]
    public void LottiePrintsCompilingPlayback() => AssertCompiles(
        "LottieView Toggle, Switch, RenderSpinner; TextBlock SwitchLabel, MarkerList; StackPanel MarkerButtons; ControlScope Scope;",
        $"{Snippets.LottieMarkers}\n{Snippets.LottieTimeline}\n{Snippets.LottieRenderThread}");

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
            using Monaco.Presto.AvaloniaBridge.Controls;
            using Monaco.Presto.AvaloniaBridge.Draggables;
            using Monaco.Presto.AvaloniaBridge.Lottie;
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
            .Concat(new[] { typeof(object), typeof(Presto), typeof(PrestoAvalonia), typeof(AvaloniaObject), typeof(Control), typeof(CodePanel), typeof(LottieView) }
                .Select(type => type.Assembly.Location))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("Scratch", [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var errors = compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, $"{string.Join(Environment.NewLine, errors)}{Environment.NewLine}{source}");
    }
}
