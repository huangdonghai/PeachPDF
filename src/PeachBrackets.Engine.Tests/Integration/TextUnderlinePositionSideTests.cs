using PeachPDF.Tests.TestSupport;
using System.Linq;
using System.Threading.Tasks;

namespace PeachPDF.Tests.Integration
{
    /// <summary>
    /// Issue #1146: <c>text-underline-position</c>'s <c>left</c>/<c>right</c> half
    /// (<see href="https://www.w3.org/TR/css-text-decor-4/#text-underline-position-property">css-text-decor-4
    /// §2.5</see>) pins the underline to a literal physical edge under a true vertical writing mode,
    /// switching a same-line overline to the opposite edge when the two would otherwise collide. See
    /// <see cref="TextUnderlineOffsetPositionTests"/> for the <c>auto</c>/<c>from-font</c>/<c>under</c>
    /// half and <c>CssUtilsTests</c> for the compound grammar's own parser tests.
    /// </summary>
    public class TextUnderlinePositionSideTests
    {
        [Theory]
        [InlineData("vertical-rl", "left")]
        [InlineData("vertical-rl", "right")]
        [InlineData("vertical-lr", "left")]
        [InlineData("vertical-lr", "right")]
        public async Task Underline_PinnedToLiteralSide_DrawsInwardFromThatPhysicalEdge(string writingMode, string side)
        {
            var (root, container) = await LayoutHarness.LayoutAsync(LayoutHarness.Wrap(
                $"<div style=\"writing-mode:{writingMode}; height:300pt\">" +
                $"<span id='s' style='text-decoration:underline; text-underline-position:{side}'>text</span></div>"),
                margin: 0);
            var s = LayoutHarness.FindById(root, "s")!;

            var g = new TestRecordingGraphics();
            FragmentPaintHarness.PaintBox(container, s, g);

            var line = Assert.Single(g.Log.OfType<TestRecordingGraphics.DrawLineCall>());
            var rect = s.Rectangles.Values.Single();
            var edge = side == "left" ? rect.Left : rect.Right;
            var inwardSign = side == "left" ? 1 : -1;

            // A true vertical mode's cross-axis coordinate (physical X) is carried on both X1 and X2 -
            // see StrokeDecorationSegment's isVertical Draw local, which calls DrawLine(pen, at, x1, at, x2).
            Assert.Equal(line.X1, line.X2, 3);
            Assert.True(inwardSign * (line.X1 - edge) > 0,
                $"expected the underline to sit inward from the pinned '{side}' edge ({edge}), got X={line.X1}");
        }

        [Theory]
        [InlineData("vertical-rl", "right")] // vertical-rl's own "over" side is physical right
        [InlineData("vertical-lr", "left")]  // vertical-lr's own "over" side is physical left
        public async Task PinningTheUnderlineToTheOverSide_SwitchesTheOverlineToTheOppositeEdge(string writingMode, string conflictingSide)
        {
            var (root, container) = await LayoutHarness.LayoutAsync(LayoutHarness.Wrap(
                $"<div style=\"writing-mode:{writingMode}; height:300pt\">" +
                "<span id='s' style='text-decoration:underline overline; " +
                $"text-underline-position:{conflictingSide}'>text</span></div>"),
                margin: 0);
            var s = LayoutHarness.FindById(root, "s")!;

            var g = new TestRecordingGraphics();
            FragmentPaintHarness.PaintBox(container, s, g);

            var lines = g.Log.OfType<TestRecordingGraphics.DrawLineCall>().ToList();
            Assert.Equal(2, lines.Count);

            var rect = s.Rectangles.Values.Single();
            var centre = rect.X + rect.Width / 2;
            var oppositeIsRight = conflictingSide == "left";

            // The underline pinned to the "over" side would collide with an unmoved overline still
            // drawn there - css-text-decor-4 §2.5 moves the overline to the opposite ("under") side
            // instead. Both anchor to their own side's em/box edge rather than the rectangle edge, so
            // the assertion is which side of the column each stroke ended up on.
            Assert.Single(lines, l => oppositeIsRight ? l.X1 > centre : l.X1 < centre);
            Assert.Single(lines, l => oppositeIsRight ? l.X1 < centre : l.X1 > centre);
        }

        [Theory]
        [InlineData("vertical-rl", "left")]  // vertical-rl's own "under" side is physical left - no conflict
        [InlineData("vertical-lr", "right")] // vertical-lr's own "under" side is physical right - no conflict
        public async Task PinningTheUnderlineToTheUnderSide_LeavesTheOverlineWhereItAlwaysWas(string writingMode, string nonConflictingSide)
        {
            var (root, container) = await LayoutHarness.LayoutAsync(LayoutHarness.Wrap(
                $"<div style=\"writing-mode:{writingMode}; height:300pt\">" +
                "<span id='s' style='text-decoration:underline overline; " +
                $"text-underline-position:{nonConflictingSide}'>text</span></div>"),
                margin: 0);
            var s = LayoutHarness.FindById(root, "s")!;

            var withSide = new TestRecordingGraphics();
            FragmentPaintHarness.PaintBox(container, s, withSide);

            var (rootAuto, containerAuto) = await LayoutHarness.LayoutAsync(LayoutHarness.Wrap(
                $"<div style=\"writing-mode:{writingMode}; height:300pt\">" +
                "<span id='s' style='text-decoration:underline overline'>text</span></div>"),
                margin: 0);
            var sAuto = LayoutHarness.FindById(rootAuto, "s")!;

            var withoutSide = new TestRecordingGraphics();
            FragmentPaintHarness.PaintBox(containerAuto, sAuto, withoutSide);

            var overlineWith = withSide.Log.OfType<TestRecordingGraphics.DrawLineCall>()
                .OrderBy(l => l.X1).First();
            var overlineWithout = withoutSide.Log.OfType<TestRecordingGraphics.DrawLineCall>()
                .OrderBy(l => l.X1).First();

            Assert.Equal(overlineWithout.X1, overlineWith.X1, 3);
        }

        [Theory]
        [InlineData("left")]
        [InlineData("right")]
        public async Task HorizontalWritingMode_SideValue_ParsesButHasNoVisibleEffect(string side)
        {
            var withoutSide = await HorizontalUnderlineXAsync("");
            var withSide = await HorizontalUnderlineXAsync($"text-underline-position:{side}");

            Assert.Equal(withoutSide, withSide, 3);
        }

        [Theory]
        [InlineData("vertical-rl", "left")]
        [InlineData("vertical-rl", "right")]
        [InlineData("vertical-lr", "left")]
        [InlineData("vertical-lr", "right")]
        public async Task TextUnderlineOffset_MovesThePinnedUnderlineFurtherFromTheText(string writingMode, string side)
        {
            // A positive text-underline-offset must move the underline further from the text - i.e.
            // further past the pinned edge (outward), not back toward the glyphs (inward), regardless of
            // which literal side it is pinned to. An earlier version of this fix had the offset and
            // clearance terms sharing the same sign, which moved the line the wrong way whenever offset
            // was non-zero (invisible when offset was 0, which is why the other tests above don't catch
            // it - see ResolveUnderlineCross's own remarks on the clearance/offset sign relationship).
            var withoutOffset = await VerticalUnderlineXAsync(writingMode, side, "");
            var withOffset = await VerticalUnderlineXAsync(writingMode, side, "text-underline-offset:5pt");

            var outwardSign = side == "left" ? -1 : 1;
            Assert.Equal(withoutOffset + outwardSign * 5, withOffset, 3);
        }

        [Theory]
        [InlineData("vertical-rl", "right")] // conflicts with vertical-rl's own "over" (physical right) side
        [InlineData("vertical-lr", "left")]  // conflicts with vertical-lr's own "over" (physical left) side
        public async Task DoubleStyle_SwitchedOverline_GrowsAwayFromTheText_NotBackIntoIt(string writingMode, string conflictingSide)
        {
            // Issue found during #1146's own review: the double-style's second stroke direction was
            // computed from the keyword name alone (overline grows toward "over"), not from where the
            // keyword's line actually ended up - so a switched overline (now physically on the "under"
            // edge) grew its second stroke back toward "over", i.e. back through the interior where the
            // pinned underline now sits, instead of continuing outward past its own new edge.
            var (root, container) = await LayoutHarness.LayoutAsync(LayoutHarness.Wrap(
                $"<div style=\"writing-mode:{writingMode}; height:300pt\">" +
                "<span id='s' style='text-decoration:underline overline; text-decoration-style:double; " +
                $"text-underline-position:{conflictingSide}'>text</span></div>"),
                margin: 0);
            var s = LayoutHarness.FindById(root, "s")!;

            var g = new TestRecordingGraphics();
            FragmentPaintHarness.PaintBox(container, s, g);

            var lines = g.Log.OfType<TestRecordingGraphics.DrawLineCall>().ToList();
            Assert.Equal(4, lines.Count); // double style: 2 strokes each for underline and overline

            var rect = s.Rectangles.Values.Single();
            var centre = rect.X + rect.Width / 2;
            var oppositeIsRight = conflictingSide == "left";

            // The switched overline's two strokes both sit on the opposite ("under") side of the
            // column, past its own edge; neither crosses back toward the pinned underline's strokes on
            // the other side.
            var overlineLines = lines.Where(l => oppositeIsRight ? l.X1 > centre : l.X1 < centre).ToList();
            var underlineLines = lines.Where(l => oppositeIsRight ? l.X1 < centre : l.X1 > centre).ToList();
            Assert.Equal(2, overlineLines.Count);
            Assert.Equal(2, underlineLines.Count);
        }

        [Theory]
        [InlineData("vertical-rl", "right", "padding-right")] // vertical-rl's block-start (over) edge is physical right
        [InlineData("vertical-lr", "left", "padding-left")]   // vertical-lr's block-start (over) edge is physical left
        public async Task PinnedUnderline_IsNotMovedByTheBoxsPadding(string writingMode, string side, string paddingProperty)
        {
            // A vertical underline anchors to the glyphs' em box, not the box's padded edge - checked
            // against Chrome at 96px LXGW, where padding-left/padding-right: 20px left the pinned
            // underline's X unchanged (and the automatic underline likewise does not move with
            // line-height or text-decoration-thickness).
            var withPadding = await VerticalUnderlineXAsync(writingMode, side, $"{paddingProperty}:20pt");
            var withoutPadding = await VerticalUnderlineXAsync(writingMode, side, "");

            Assert.Equal(withoutPadding, withPadding, 3);
        }

        [Theory]
        [InlineData("vertical-rl", "right")] // vertical-rl's own "over" side is physical right
        [InlineData("vertical-lr", "left")]  // vertical-lr's own "over" side is physical left
        public async Task OverlineAlone_WithAConflictingSideValue_DoesNotSwitch_NoUnderlineToCollideWith(string writingMode, string conflictingSide)
        {
            // The spec's own switch note ("if this causes the underline to be drawn on the over side...")
            // is conditioned on an underline actually being drawn - a box with only "overline" declared
            // has no underline to collide with, so text-underline-position must not move its overline.
            var (root, container) = await LayoutHarness.LayoutAsync(LayoutHarness.Wrap(
                $"<div style=\"writing-mode:{writingMode}; height:300pt\">" +
                $"<span id='s' style='text-decoration:overline; text-underline-position:{conflictingSide}'>text</span></div>"),
                margin: 0);
            var s = LayoutHarness.FindById(root, "s")!;

            var g = new TestRecordingGraphics();
            FragmentPaintHarness.PaintBox(container, s, g);

            var line = Assert.Single(g.Log.OfType<TestRecordingGraphics.DrawLineCall>());
            var rect = s.Rectangles.Values.Single();

            // An overline (with or without a conflicting side value on an absent underline) stays on the
            // box's own over side and grows outward from that edge.
            if (writingMode == "vertical-rl")
            {
                Assert.True(line.X1 > rect.Right,
                    $"vertical-rl's overline should grow outward from the box's right edge ({rect.Right}), got X={line.X1}");
            }
            else
            {
                Assert.True(line.X1 < rect.Left,
                    $"vertical-lr's overline should grow outward from the box's left edge ({rect.Left}), got X={line.X1}");
            }
        }

        [Fact]
        public async Task InheritedThroughTheFullCascade_SurvivesOnANonDeclaringDescendant()
        {
            // Regression for the customSetter/RMW shape this property shares with position/
            // RunningElementName: TextUnderlineSide has no css-properties.json entry of its own, so it
            // is never independently reasserted by DomParser.CascadeApplyStyles's defaulting loop - but
            // proving that needs the REAL cascade (CascadeApplyStyles then CssBox.InheritStyle), not a
            // direct CssUtils.SetPropertyValue call, which bypasses both entirely.
            var (root, container) = await LayoutHarness.LayoutAsync(LayoutHarness.Wrap(
                "<div id='parent' style='text-underline-position:under left'>" +
                "<span id='child' style='color:red'>text</span></div>"),
                margin: 0);
            _ = container;
            var child = LayoutHarness.FindById(root, "child")!;

            Assert.Equal(global::PeachBrackets.Css.TextUnderlinePosition.Under, child.TextUnderlinePosition.Value);
            Assert.Equal(global::PeachBrackets.Css.TextUnderlineSide.Left, child.TextUnderlineSide.Value);
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────

        private static async Task<double> VerticalUnderlineXAsync(string writingMode, string side, string extraStyle)
        {
            var style = string.IsNullOrEmpty(extraStyle)
                ? $"text-decoration:underline; text-underline-position:{side}"
                : $"text-decoration:underline; text-underline-position:{side}; {extraStyle}";
            var (root, container) = await LayoutHarness.LayoutAsync(LayoutHarness.Wrap(
                $"<div style=\"writing-mode:{writingMode}; height:300pt\">" +
                $"<span id='s' style='{style}'>text</span></div>"),
                margin: 0);
            var s = LayoutHarness.FindById(root, "s")!;

            var g = new TestRecordingGraphics();
            FragmentPaintHarness.PaintBox(container, s, g);

            return Assert.Single(g.Log.OfType<TestRecordingGraphics.DrawLineCall>()).X1;
        }

        private static async Task<double> HorizontalUnderlineXAsync(string extraStyle)
        {
            var style = string.IsNullOrEmpty(extraStyle)
                ? "text-decoration:underline"
                : $"text-decoration:underline; {extraStyle}";
            var (root, container) = await LayoutHarness.LayoutAsync(
                LayoutHarness.Wrap($"<span id='s' style='{style}'>text</span>"), margin: 0);
            var s = LayoutHarness.FindById(root, "s")!;

            var g = new TestRecordingGraphics();
            FragmentPaintHarness.PaintBox(container, s, g);

            // Horizontal decoration geometry carries its cross-axis coordinate on Y (see
            // TextUnderlineOffsetPositionTests) - X1 here is only the span start, which text-underline-
            // position never moves, but reading Y proves the position itself is unaffected by 'left'/
            // 'right' the same way X1 would for the span.
            return Assert.Single(g.Log.OfType<TestRecordingGraphics.DrawLineCall>()).Y1;
        }
    }
}
