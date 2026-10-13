using PeachBrackets.Html.Dom;
using PeachPDF.Tests.TestSupport;
using System;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace PeachPDF.Tests.Integration
{
    public class MarkerImageSizingIntegrationTests
    {
        // ─── Marker sizing (css-lists-3 §3.3) ───────────────────────────────────────────────────────
        // The marker image is sized by the default sizing algorithm with no specified size and a
        // default object size of 1em square, and the marker box is exactly what that resolves to - so
        // the image is never stretched to, nor clipped by, a box the font describes instead of the
        // image.

        private static string SvgDataUri(string markup) =>
            "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(markup));

        [Fact]
        public async Task MarkerImage_Gradient_ReservesExactlyOneEmNotTheFontLineSpacing()
        {
            // A gradient is a "generated image": no intrinsic size, no intrinsic ratio, so it takes the
            // default object size wholesale - an exact em square, i.e. the computed font-size. The font's
            // *line spacing* (ActualFont.Height: ~1.15em for the real fallback face) is not it, and is
            // measurably larger, which is what made a gradient marker render visibly bigger than Chrome.
            var (root, _) = await LayoutHarness.LayoutAsync(LayoutHarness.Wrap(
                "<ul style='margin: 0; font-size: 20pt; list-style-image: linear-gradient(to right, red, blue)'>" +
                "<li id='i'>Item one</li></ul>"));

            var word = Assert.Single(MarkerOf(root, "i").Words);

            Assert.Equal(20d, word.Width, 3);
            Assert.Equal(20d, word.Height, 3);
        }

        [Fact]
        public async Task MarkerImage_SvgUrl_ReservesItsOwnIntrinsicSizeRatherThanOneEm()
        {
            // 40x30 user units are CSS pixels (1px = 0.75pt), so this image is larger than the 10pt em
            // it sits in - and it must still reserve exactly its own size, neither scaled down to the em
            // nor clipped to it.
            var wide = SvgDataUri("""
                <svg xmlns="http://www.w3.org/2000/svg" width="40" height="30">
                  <rect width="40" height="30" fill="#C0392B"/>
                </svg>
                """);

            var (root, _) = await LayoutHarness.LayoutAsync(LayoutHarness.Wrap(
                $"<ul style='margin: 0; font-size: 10pt; list-style-image: url(\"{wide}\")'>" +
                "<li id='i'>Item one</li></ul>"));

            var word = Assert.Single(MarkerOf(root, "i").Words);

            Assert.Equal(40d * 0.75, word.Width, 3);
            Assert.Equal(30d * 0.75, word.Height, 3);
        }

        [Fact]
        public async Task MarkerImage_ViewBoxOnlySvg_IsContainedInTheOneEmDefaultObjectSize()
        {
            // A viewBox is an intrinsic *ratio*, not an intrinsic size (css-images-3 §4.2.1), so a
            // document with no width/height is sized by the default sizing algorithm's "ratio but no
            // dimensions" branch: contained in the 1em default object size. 10x10 user units are 7.5pt at
            // 1px = 0.75pt, which is *smaller* than the 10pt em they sit in, so a box taken from the
            // viewBox extent and a box taken from the em square are far enough apart to tell apart.
            var square = SvgDataUri("""
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10">
                  <rect width="10" height="10" fill="#C0392B"/>
                </svg>
                """);

            var (root, _) = await LayoutHarness.LayoutAsync(LayoutHarness.Wrap(
                $"<ul style='margin: 0; font-size: 10pt; list-style-image: url(\"{square}\")'>" +
                "<li id='i'>Item one</li></ul>"));

            var word = Assert.Single(MarkerOf(root, "i").Words);

            Assert.Equal(10d, word.Width, 3);
            Assert.Equal(10d, word.Height, 3);
        }

        [Fact]
        public async Task MarkerImage_ViewBoxOnlySvg_WithNonSquareRatio_KeepsThatRatioInsideTheEm()
        {
            // Containment, not stretching: a 2:1 viewBox fills the em's width and takes half its height.
            var wide = SvgDataUri("""
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 10">
                  <rect width="20" height="10" fill="#C0392B"/>
                </svg>
                """);

            var (root, _) = await LayoutHarness.LayoutAsync(LayoutHarness.Wrap(
                $"<ul style='margin: 0; font-size: 10pt; list-style-image: url(\"{wide}\")'>" +
                "<li id='i'>Item one</li></ul>"));

            var word = Assert.Single(MarkerOf(root, "i").Words);

            Assert.Equal(10d, word.Width, 3);
            Assert.Equal(5d, word.Height, 3);
        }

        private static CssBoxMarker MarkerOf(CssBox root, string listItemId)
        {
            var item = LayoutHarness.FindById(root, listItemId);
            Assert.NotNull(item);
            return (CssBoxMarker)Assert.Single(item!.Boxes, b => b.IsMarkerPseudoElement);
        }
    }
}
