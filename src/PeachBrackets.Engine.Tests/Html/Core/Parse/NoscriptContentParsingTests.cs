using PeachBrackets.Html.Dom;
using PeachBrackets.Html.Parse;
using System;
using System.Linq;
using Xunit;

namespace PeachPDF.Tests.Html.Core.Parse
{
    /// <summary>
    /// How <c>&lt;noscript&gt;</c> content is parsed: this renderer runs no script, so the element's
    /// content is live markup, not raw text - <see href="https://html.spec.whatwg.org/multipage/parsing.html#parsing-main-inbody">HTML
    /// §13.2.6.4.7 "in body"</see> processes a noscript element's tokens as any other markup when
    /// scripting is disabled.
    ///
    /// <para>
    /// The parser used to send the text directly inside the element back through itself with the
    /// noscript box as the parse root. The box was still the noscript element in that recursive call,
    /// so the same text met the same branch again: it recursed until the stack was gone and took the
    /// whole process down. A page holding nothing but <c>&lt;noscript&gt;fallback text&lt;/noscript&gt;</c>
    /// was enough. These tests pin the tree the fix builds.
    /// </para>
    /// </summary>
    public class NoscriptContentParsingTests
    {
        [Fact]
        public void BareTextInsideNoscript_BecomesATextBoxInsideTheElement()
        {
            var noscript = NoscriptOf("<noscript id='t'>plain</noscript>");

            Assert.Equal(["plain"], noscript.Boxes.Where(b => b.HtmlTag is null).Select(b => b.Text));
        }

        [Fact]
        public void MarkupInsideNoscript_BecomesBoxes()
        {
            var noscript = NoscriptOf("<noscript id='t'><p>fallback</p></noscript>");

            Assert.Equal(["p"], noscript.Boxes.Where(b => b.HtmlTag is not null).Select(b => b.HtmlTag!.Name.ToLowerInvariant()));
        }

        [Fact]
        public void TextAfterMarkupInsideNoscript_KeepsBoth()
        {
            var noscript = NoscriptOf("<noscript id='t'><b>x</b> tail</noscript>");

            Assert.Contains(noscript.Boxes, b => b.HtmlTag?.Name.Equals("b", StringComparison.OrdinalIgnoreCase) == true);
            Assert.Contains(noscript.Boxes, b => b.HtmlTag is null && b.Text?.Trim() == "tail");
        }

        private static CssBox NoscriptOf(string bodyHtml) =>
            FindById(HtmlParser.ParseDocument($"<!DOCTYPE html><html><body>{bodyHtml}</body></html>"), "t")!;

        private static CssBox? FindById(CssBox box, string id)
        {
            if (box.HtmlTag?.TryGetAttribute("id") == id) return box;

            foreach (var child in box.Boxes)
            {
                var found = FindById(child, id);
                if (found != null) return found;
            }

            return null;
        }
    }
}
