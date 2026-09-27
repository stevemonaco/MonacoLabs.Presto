using Avalonia.Headless;
using PrestoSample.Tests;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]
