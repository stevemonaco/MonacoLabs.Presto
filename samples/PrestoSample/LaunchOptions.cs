using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using PrestoSample.ViewModels;

namespace PrestoSample;

/// <summary>Command-line options that open the gallery in a known state, so manual and automated checks start from the same place.</summary>
public sealed record LaunchOptions(GalleryPage? Page = null, MotionPreference? Motion = null, int? Width = null, int? Height = null)
{
    public static string Usage =>
        """
        Options:
          --page <key>                 Open a page at launch: its slug (see --list-pages) or its title, case-insensitive.
          --motion full|reduced|system Set the gallery's motion preference.
          --size <width>x<height>      Set the window size in DIPs, e.g. 1400x1000.
          --list-pages                 Print every page's slug and title, then exit.
        """;

    /// <summary>Parses <paramref name="args"/>, accepting <c>--name value</c> and <c>--name=value</c>.</summary>
    /// <returns>False with <paramref name="error"/> set when an argument is unknown or malformed.</returns>
    public static bool TryParse(IReadOnlyList<string> args, out LaunchOptions options, out bool listPages, out string? error)
    {
        options = new LaunchOptions();
        listPages = false;
        error = null;
        for (var i = 0; i < args.Count; i++)
        {
            var (name, value) = args[i].Split('=', 2) is [var n, var v] ? (n, (string?)v) : (args[i], null);
            if (name == "--list-pages")
            {
                listPages = true;
                continue;
            }
            if (name is not ("--page" or "--motion" or "--size"))
            {
                error = $"Unknown argument '{args[i]}'.";
                return false;
            }
            value ??= i + 1 < args.Count ? args[++i] : null;
            if (string.IsNullOrWhiteSpace(value))
            {
                error = $"{name} needs a value.";
                return false;
            }
            switch (name)
            {
                case "--page":
                    if (FindPage(value) is not { } page)
                    {
                        error = $"No page matches '{value}'. Run with --list-pages to see them.";
                        return false;
                    }
                    options = options with { Page = page };
                    break;
                case "--motion":
                    if (!Enum.TryParse<MotionPreference>(value, ignoreCase: true, out var motion) || !Enum.IsDefined(motion))
                    {
                        error = $"--motion must be full, reduced or system, not '{value}'.";
                        return false;
                    }
                    options = options with { Motion = motion };
                    break;
                default:
                    if (value.Split('x', 'X') is not [var w, var h]
                        || !int.TryParse(w, NumberStyles.None, CultureInfo.InvariantCulture, out var width)
                        || !int.TryParse(h, NumberStyles.None, CultureInfo.InvariantCulture, out var height)
                        || width <= 0 || height <= 0)
                    {
                        error = $"--size must be <width>x<height>, not '{value}'.";
                        return false;
                    }
                    options = options with { Width = width, Height = height };
                    break;
            }
        }
        return true;
    }

    public static IEnumerable<string> PageList() =>
        MainViewModel.Catalog.SelectMany(section => section.Pages).Select(page => $"{Slug(page.Title),-28} {page.Section} / {page.Title}");

    private static GalleryPage? FindPage(string key) =>
        MainViewModel.Catalog.SelectMany(section => section.Pages).FirstOrDefault(page => Slug(page.Title) == Slug(key));

    /// <summary>Lower-case letters and digits, with every other run of characters as one hyphen: "Connected XAML" is "connected-xaml".</summary>
    public static string Slug(string text)
    {
        var slug = new StringBuilder(text.Length);
        foreach (var c in text.Trim())
        {
            if (char.IsLetterOrDigit(c)) slug.Append(char.ToLowerInvariant(c));
            else if (slug.Length > 0 && slug[^1] != '-') slug.Append('-');
        }
        return slug.ToString().TrimEnd('-');
    }
}
