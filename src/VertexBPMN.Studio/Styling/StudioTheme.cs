using MudBlazor;

namespace VertexBPMN.Studio.Styling;

internal static class StudioTheme
{
    private static readonly string[] FontFamily =
    [
        "Segoe UI",
        "system-ui",
        "-apple-system",
        "BlinkMacSystemFont",
        "sans-serif"
    ];

    public static MudTheme Default { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#2563EB",
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#475569",
            SecondaryContrastText = "#FFFFFF",
            Tertiary = "#64748B",
            Background = "#F6F8FB",
            BackgroundGray = "#EEF2F7",
            Surface = "#FFFFFF",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#172033",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#334155",
            TextPrimary = "#172033",
            TextSecondary = "#64748B",
            LinesDefault = "#DCE3EC",
            LinesInputs = "#CBD5E1",
            ActionDefault = "#475569",
            ActionDisabled = "#94A3B8",
            ActionDisabledBackground = "#E2E8F0",
            Success = "#15803D",
            Warning = "#B45309",
            Error = "#B91C1C",
            Info = "#0369A1"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = FontFamily,
                FontSize = ".875rem",
                LineHeight = "1.5"
            },
            H1 = new H1Typography
            {
                FontFamily = FontFamily,
                FontSize = "2rem",
                FontWeight = "700",
                LineHeight = "1.2",
                LetterSpacing = "-.025em"
            },
            H2 = new H2Typography
            {
                FontFamily = FontFamily,
                FontSize = "1.5rem",
                FontWeight = "700",
                LineHeight = "1.25",
                LetterSpacing = "-.02em"
            },
            H3 = new H3Typography
            {
                FontFamily = FontFamily,
                FontSize = "1.25rem",
                FontWeight = "650",
                LineHeight = "1.3"
            },
            H4 = new H4Typography
            {
                FontFamily = FontFamily,
                FontSize = "1.125rem",
                FontWeight = "650",
                LineHeight = "1.35"
            },
            H5 = new H5Typography
            {
                FontFamily = FontFamily,
                FontSize = "1rem",
                FontWeight = "650",
                LineHeight = "1.4"
            },
            H6 = new H6Typography
            {
                FontFamily = FontFamily,
                FontSize = ".875rem",
                FontWeight = "650",
                LineHeight = "1.4"
            },
            Button = new ButtonTypography
            {
                FontFamily = FontFamily,
                FontSize = ".8125rem",
                FontWeight = "650",
                LetterSpacing = ".01em",
                TextTransform = "none"
            }
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px"
        }
    };
}
