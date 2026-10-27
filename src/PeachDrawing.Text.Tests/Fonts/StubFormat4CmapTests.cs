using PeachDrawing.Text.Internal.Fonts;
using PeachDrawing.Text.Shaping;
using PeachPDF.Tests.TestSupport;
using System.IO;
using System.Text;

namespace PeachDrawing.Text.Tests.Fonts
{
    /// <summary>
    /// A font whose format-4 cmap is a stub - the two segments every format-4 subtable needs, mapping
    /// nothing beyond U+0000 and the U+FFFF terminator - while its real Unicode mapping lives in the
    /// format-12 subtable, is still usable for Basic Multilingual Plane text. Some CJK fonts ship exactly
    /// this shape (LXGW WenKai Light among them), and a format-4 miss that does not fall through to the
    /// format-12 groups maps every character of such a font to .notdef: browsers pick the format-12
    /// subtable, so the font renders there, and the engine's per-codepoint fallback silently routes the
    /// text to a system font instead.
    /// </summary>
    public class StubFormat4CmapTests
    {
        [Fact]
        public void BmpCharactersResolveThroughTheFormat12Groups_WhenFormat4IsAStub()
        {
            var bytes = Format12OnlyFontTests.Build(File.ReadAllBytes(BundledFonts.Ttf), out var original, stubFormat4: true);
            var face = FontFileData.GetOrCreateFrom(bytes).Fontface;

            Assert.NotNull(face.cmap.cmap4);
            Assert.NotNull(face.cmap.cmap12);

            var typeface = TypefaceFixtures.FromBytes(bytes);
            Assert.Equal(original.GlyphOf('A'), typeface.GlyphOf('A'));
            Assert.Equal(original.GlyphOf('z'), typeface.GlyphOf('z'));
            Assert.True(typeface.HasGlyph(new Rune('Q')));

            // Unmapped codepoints stay the missing glyph, and the stub's terminator is not coverage.
            Assert.Equal(0, typeface.GlyphOf('1'));
            Assert.False(typeface.HasGlyph(new Rune(0xFFFF)));
        }

        [Fact]
        public void TheFaceCoverageCheckSeesTheFormat12Groups()
        {
            var bytes = Format12OnlyFontTests.Build(File.ReadAllBytes(BundledFonts.Ttf), out _, stubFormat4: true);
            var family = new FontSet().AddData(bytes);

            Assert.True(family.TryMatch(new TypefaceQuery { MustCover = new Rune('Q') }, out _));
            Assert.False(family.TryMatch(new TypefaceQuery { MustCover = new Rune('1') }, out _));
        }

        [Fact]
        public void ItShapesText()
        {
            var bytes = Format12OnlyFontTests.Build(File.ReadAllBytes(BundledFonts.Ttf), out var original, stubFormat4: true);
            var typeface = TypefaceFixtures.FromBytes(bytes);

            var run = Shaper.Shape(typeface, "Hi", ShapeSettings.Default);

            Assert.Equal(2, run.Glyphs.Count);
            Assert.Equal(original.GlyphOf('H'), run.Glyphs[0].GlyphIndex);
        }
    }
}
