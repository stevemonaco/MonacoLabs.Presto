using Avalonia.Headless;
using MonacoLabs.Presto.Tests;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]
