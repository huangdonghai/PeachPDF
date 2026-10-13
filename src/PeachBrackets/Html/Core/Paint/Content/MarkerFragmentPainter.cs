using PeachBrackets.Css;
using PeachDrawing.Core;
using PeachBrackets.Html.Dom;
using PeachBrackets.Html.Fragments;
using PeachBrackets.Html.Handlers;
using PeachBrackets.Html.Utils;

namespace PeachBrackets.Html.Paint.Content
{
    /// <summary>
    /// Paints a synthesized <c>::marker</c> pseudo-element: a marker image, one of the procedural
    /// <c>disc</c>/<c>circle</c>/<c>square</c> shapes drawn as vector geometry, or the formatted
    /// counter text.
    /// </summary>
    internal sealed class MarkerFragmentPainter : IFragmentContentPainter
    {
        public void Paint(FragmentPainter painter, Canvas g, BoxFragment fragment)
        {
            var box = (CssBoxMarker)fragment.Box;

            if (box.ContentImage is not null)
            {
                var rect = fragment.PrimaryRect;
                if (rect is { Width: > 0, Height: > 0 })
                {
                    PaintMarkerImage(g, box, rect);
                }

                return;
            }

            if (box.Words.Count == 0) return; // content: none, or list-style-type: none with no image

            // A marker whose own word fell outside this page's band has nothing to draw here.
            if (!fragment.TryGetWordRect(box.Words[0], out var wordRect)) return;

            if (box.MarkerShape is not null)
            {
                PaintShape(g, box, wordRect);
            }
            else
            {
                g.DrawString(box.Text ?? string.Empty, box.ActualFont, box.ActualColor,
                    new PaintPoint(wordRect.X, wordRect.Y), new Size(wordRect.Width, wordRect.Height),
                    letterSpacing: 0, fontPalette: null, box.ActualTextShapingFeatures);
            }
        }

        private static void PaintMarkerImage(Canvas g, CssBoxMarker box, Rect rect)
        {
            CssImagePainter.Paint(g, box.ContentImage!, layerIndex: 0,
                originRect: rect, clipRect: rect, roundedClipPath: null,
                // A borrowed list-style-image marker's box is sized from the image itself
                // (CssBoxMarker.ResolveImageSize, css-lists-3 §3.3), so centering the image within it is
                // a no-op and the layer is asked for exactly that box - the image is neither stretched to
                // nor clipped by it. "100% 100%" rather than "auto" because the two only agree while the
                // painter can re-derive the size the box was cut from: an SVG with a viewBox but no
                // width/height resolves to the viewBox extent in the paint paths (SvgIntrinsicSize.Resolve)
                // but to a 1em-contained ratio in the box, and the box is the one that is right. An author
                // content:url(...) override (owned) keeps ::before/::after's own top-left content-image
                // alignment and its ordinary "auto" sizing.
                positionList: box.OwnsContentImage ? "0% 0%" : "center",
                sizeList: box.OwnsContentImage ? Keywords.Auto : "100% 100%", repeatList: "no-repeat",
                attachmentList: Keywords.Scroll, viewportRect: rect, box: box,
                drawBrush: brush =>
                {
                    g.DrawRectangle(brush, rect.X, rect.Y, rect.Width, rect.Height);
                    brush.Dispose();
                });
        }

        private static void PaintShape(Canvas g, CssBoxMarker box, Rect rect)
        {
            if (box.MarkerShape == Keywords.Square)
            {
                using var squareBrush = g.GetSolidBrush(box.ActualColor);
                g.DrawRectangle(squareBrush, rect.X, rect.Y, rect.Width, rect.Height);
                return;
            }

            var rx = rect.Width / 2;
            var ry = rect.Height / 2;
            using var path = RenderUtils.GetRoundRect(g, rect, rx, ry, rx, ry, rx, ry, rx, ry);

            if (box.MarkerShape == Keywords.Disc)
            {
                using var brush = g.GetSolidBrush(box.ActualColor);
                g.DrawPath(brush, path);
            }
            else // circle: hollow ring
            {
                var pen = g.GetPen(box.ActualColor);
                g.DrawPath(pen, path);
            }
        }
    }
}
