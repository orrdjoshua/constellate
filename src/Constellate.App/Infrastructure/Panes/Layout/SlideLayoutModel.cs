using System;
using System.Collections.Generic;

namespace Constellate.App.Infrastructure.Panes.Layout
{
    public sealed record SlideLayoutModel(
        int SlideIndex,
        IReadOnlyList<LaneLayoutModel> Lanes,
        bool IsScrollableAcrossSlides = false)
    {
        public static SlideLayoutModel Empty(int slideIndex)
        {
            return new SlideLayoutModel(
                SlideIndex: slideIndex,
                Lanes: Array.Empty<LaneLayoutModel>(),
                IsScrollableAcrossSlides: false);
        }
    }
}
