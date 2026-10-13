// "Therefore those skilled at the unorthodox
// are infinite as heaven and earth,
// inexhaustible as the great rivers.
// When they come to an end,
// they begin again,
// like the days and months;
// they die and are reborn,
// like the four seasons."
//
// - Sun Tsu,
// "The Art of War"

using PeachBrackets.Html.Dom;

namespace PeachBrackets.Svg
{
    /// <summary>
    /// Resolves an <see cref="SvgDocument"/>'s own natural size (independent of any CSS box sizing),
    /// for feeding into <c>CssLayoutEngine.MeasureIntrinsicSize</c> - shared by
    /// <c>CssBoxSvg.MeasureWordsSize</c> (inline <c>&lt;svg&gt;</c>) and
    /// <c>CssBoxImage.MeasureWordsSize</c> (<c>&lt;img src="x.svg"&gt;</c>).
    /// </summary>
    internal static class SvgIntrinsicSize
    {
        public static (double? Width, double? Height) Resolve(SvgDocument? document)
        {
            if (document is null)
                return (null, null);

            var width = document.Width ?? document.ViewBox?.Width;
            var height = document.Height ?? document.ViewBox?.Height;

            return (width, height);
        }

        /// <summary>
        /// <see cref="Resolve"/> for sizing a replaced element's box: a document with a size in one axis only
        /// (a <c>width</c> and no <c>height</c>, or the reverse, and no <c>viewBox</c> to derive it from) takes
        /// the default object size for the other axis, 300x150 CSS px, as CSS Images 3 §5.1 gives an object with
        /// no size of its own. <c>HasRatio</c> is false for that case: nothing links its axes, so a box sized
        /// along one must leave the other at its default rather than scale it.
        /// </summary>
        public static (double? Width, double? Height, bool HasRatio) ResolveForBox(SvgDocument? document)
        {
            var (width, height) = Resolve(document);

            if (width is null || height is null)
            {
                if (width is not null) height = CssLayoutEngine.DefaultObjectHeight;
                else if (height is not null) width = CssLayoutEngine.DefaultObjectWidth;
            }

            var hasRatio = document is not null
                && (document.Width is > 0 && document.Height is > 0 || document.ViewBox is { Width: > 0, Height: > 0 });

            return (width, height, hasRatio);
        }

        /// <summary>
        /// Whether <paramref name="document"/> has a natural size in the given axis, as opposed to only a
        /// preferred aspect ratio: its own <c>width</c>/<c>height</c> attribute, or the other one plus a
        /// <c>viewBox</c> to derive it from. A <c>viewBox</c>-only document has a ratio but no natural size
        /// (<see cref="Resolve"/> still sizes it from the viewBox so it has something to lay out at), which
        /// is what a grid needs to know before letting it keep that size under <c>justify-self: normal</c>.
        /// </summary>
        public static bool HasNaturalSize(SvgDocument? document, bool horizontal)
        {
            if (document is null)
                return false;

            // A size in either axis gives the other the default object size (see ResolveForBox).
            return document.Width is > 0 || document.Height is > 0;
        }

        /// <summary>
        /// The document's intrinsic <i>dimensions</i>: its root <c>width</c>/<c>height</c> attributes,
        /// and nothing else. A <c>viewBox</c> does not appear here because, per css-images-3 §4.2.1, a
        /// document that gives only a <c>viewBox</c> has an intrinsic <i>ratio</i> but no intrinsic
        /// dimensions - a distinction <see cref="Resolve"/> deliberately erases (it falls back to the
        /// viewBox extent) and which only a caller applying the default sizing algorithm cares about.
        /// </summary>
        public static (double? Width, double? Height) IntrinsicDimensions(SvgDocument? document)
        {
            if (document is null)
                return (null, null);

            return (document.Width is > 0 ? document.Width : null,
                    document.Height is > 0 ? document.Height : null);
        }

        /// <summary>
        /// The document's intrinsic ratio: its root <c>width</c>/<c>height</c> when both are present,
        /// otherwise its <c>viewBox</c>. Null when the document declares neither, so the caller can tell
        /// "no size information at all" from "ratio only".
        /// </summary>
        public static double? IntrinsicRatio(SvgDocument? document)
        {
            if (document is null)
                return null;

            if (document.Width is { } width and > 0 && document.Height is { } height and > 0)
                return width / height;

            if (document.ViewBox is { Width: > 0, Height: > 0 } viewBox)
                return viewBox.Width / viewBox.Height;

            return null;
        }
    }
}
