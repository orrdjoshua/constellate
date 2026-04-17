namespace Constellate.App.Controls.Panes
{
    public enum PaneChromeRegion
    {
        None = 0,
        Header = 1,
        Label = 2,
        EmptyHeader = 3,
        CommandBar = 4,
        Body = 5,
        BodyEmptySurface = 6,
        MinimizedChrome = 7
    }

    public static class PaneChromeRegionNames
    {
        public const string Root = "PART_PaneChromeRoot";
        public const string Header = "PART_Header";
        public const string Label = "PART_LabelRegion";
        public const string EmptyHeader = "PART_EmptyHeaderRegion";
        public const string CommandBar = "PART_CommandBarRegion";
        public const string Body = "PART_BodyRegion";
    }

    public static class PaneChromeRegionRules
    {
        public static bool IsDragOrigin(PaneChromeRegion region)
        {
            return region == PaneChromeRegion.Label ||
                   region == PaneChromeRegion.EmptyHeader ||
                   region == PaneChromeRegion.BodyEmptySurface ||
                   region == PaneChromeRegion.MinimizedChrome;
        }

        public static bool SupportsDragHover(PaneChromeRegion region)
        {
            return IsDragOrigin(region);
        }

        public static bool IsCommandOwningRegion(PaneChromeRegion region)
        {
            return region == PaneChromeRegion.CommandBar;
        }

        public static bool IsHeaderRegion(PaneChromeRegion region)
        {
            return region == PaneChromeRegion.Header ||
                   region == PaneChromeRegion.Label ||
                   region == PaneChromeRegion.EmptyHeader ||
                   region == PaneChromeRegion.CommandBar;
        }
    }
}
