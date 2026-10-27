# A stub format-4 cmap falls through to the format-12 subtable

## What was wrong

`OpenTypeDescriptor.CharCodeToGlyphIndexCore` read the format-4 subtable for every BMP codepoint and consulted
the format-12 subtable only for astral ones. Some fonts ship a **stub** format-4 subtable - the two segments
every format-4 subtable must have (0..0 and the FFFF terminator) and nothing else - while the real Unicode
mapping lives in the format-12 (3, 10) subtable. LXGW WenKai Light, the face an `@font-face local("LXGW WenKai
Light")` declaration in the bundled 史記 fixture resolves `kt` to, is exactly that: a 32-byte format-4 subtable
with two segments, and a format-12 subtable with 3457 groups that maps the BMP as well.

Browsers and Skia prefer the full-repertoire (3, 10) subtable and render such a face normally. The engine
answered glyph 0 for every BMP character of it, so `Typeface.HasGlyph` was false for all of them. Nothing failed
loudly: `FontFamilyResolver`'s per-codepoint step asks each declared family, sees `HasGlyph` false, and walks on
to the last-resort system fallback. In SoftHtml that painted a whole book page's note text in system CJK fonts
while the box's own `ActualFont` was the declared face - the "LXGW isn't selected" report. The coverage
pre-check disagreed with the lookup for the same reason on the other side: `CmapProbe` unions every subtable,
so it correctly said "kt covers this", and the mismatch sent the investigation to the resolver before the
descriptor.

## The change

`CharCodeToGlyphIndexCore` tries format 4 for BMP codepoints and, when that maps nothing, falls through to the
format-12 groups for the same codepoint. The astral path and format-12-only fonts are unchanged; only a
format-4 miss now costs a binary search in format 12, and a font without a format-12 subtable answers exactly
as before. The format-4 segment loop moved verbatim into `MapFormat4` - it predates this library's port and is
kept byte-for-byte rather than rewritten in the same change.

Deliberately not done: no new "pick the best subtable up front" logic in `CMapTable.Read`. Trying format 4
first keeps the common font on its fastest path, and the union-of-subtables view coverage needs already exists
in `CmapProbe`.

## Evidence

- The font's own bytes: cmap records (0, 3) and (3, 1) at offset 112, format 4, `length` 32, `segCount` 2;
  (0, 4) and (3, 10) at offset 144, format 12, 3457 groups, first group 20..7E, and U+4E00 inside group 262
  (439C..69E8).
- A probe against the installed face: `Typeface.HasGlyph` was false for U+0021, U+3010, U+4E00, U+7D22 and
  U+8FF0 before the change and true after it.
- The 史記 fixture page (`OEBPS/Text/part0015.xhtml`, first sheet 2000x842pt, 1408 painted words): the SoftHtml
  text layer's note words moved from system-font hashes to LXGW - `ac136ce2... x745`, against SkiaHtml's own
  LXGW groups on the same page, `LXGW WenKai x631 + x66 + x48 = 745`. The other groups match too (`st` x654 vs
  SkiaHtml's TsukuMin x650 + x5, `h2` x8 vs x8, one last-resort character x1).
- `PeachDrawing.Text.Tests` 13162 passed and `PeachBrackets.Engine.Tests` 32114 passed (both TFMs). The new
  `StubFormat4CmapTests` derives the shape from the bundled Source Sans by replacing its cmap with a stub
  format 4 plus a real format 12, and pins the glyph lookup, face coverage and shaping.
