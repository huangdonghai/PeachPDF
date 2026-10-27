using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace PeachDrawing.Text.Internal.Fonts.OpenType
{
    /// <summary>
    /// Answers a <c>cmap</c> table's coverage - does any subtable map a code point, and how much of a range
    /// set does it cover - straight from the table's bytes, without building an <see cref="OpenTypeFontface"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The fallback search (<see cref="FontResolver"/>'s last-resort step) asks every installed face
    /// whether it covers one character. Reading each face in full to answer that - all of its tables,
    /// tens of milliseconds for a large CJK font and hundreds of faces per character - made the first
    /// fallback of a document dominate its load time; the probe needs only the two subtable kinds the
    /// reader's own coverage extraction uses (format 4 for the BMP, format 12 for astral planes) and the
    /// glyph-to-codepoint mapping detail it keeps: a code point inside a segment counts as covered, holes
    /// mapped to glyph 0 included, exactly as <see cref="CMapCoverage.Extract(CMapTable?)"/> reports them.
    /// Callers hand it the <c>cmap</c> table alone, a few tens of kilobytes, not the whole font.
    /// </para>
    /// <para>
    /// Every read is bounds-checked against the table's own bytes, and a malformed or truncated table makes
    /// the probe answer "no coverage" rather than throw - a damaged font must not break a document that
    /// merely asks it a question.
    /// </para>
    /// </remarks>
    internal static class CmapProbe
    {
        private const uint CmapTag = 0x636D6170; // 'cmap'
        private const uint CollectionTag = 0x74746366; // 'ttcf'
        private const int Format4Terminator = 0xFFFF;

        /// <summary>
        /// The subtable layout of a <c>cmap</c> table, read once per table buffer: the fallback search
        /// probes the same faces for every uncovered character, and rescanning each table's encoding
        /// records on every probe was part of its cost. Keyed by the buffer's identity (the resolver hands
        /// out one stable buffer per face).
        /// </summary>
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<byte[], Layout> Layouts = new();

        private sealed class Layout
        {
            public required (int Offset, int Format)[] Subtables { get; init; }
        }

        private static Layout LayoutOf(byte[] cmap) => Layouts.GetValue(cmap, static t => new Layout { Subtables = ReadSubtables(t) });

        /// <summary>Whether any subtable of <paramref name="cmap"/> maps <paramref name="codepoint"/>.</summary>
        public static bool Covers(byte[] cmap, int codepoint)
        {
            try
            {
                foreach (var (subtable, format) in LayoutOf(cmap).Subtables)
                {
                    if (format == 12 && CoversFormat12(cmap, subtable, codepoint))
                        return true;
                    if (format == 4 && CoversFormat4(cmap, subtable, codepoint))
                        return true;
                }
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException or ArgumentException)
            {
            }

            return false;
        }

        /// <summary>
        /// How many code points <paramref name="cmap"/> covers inside the intervals of
        /// <paramref name="ranges"/> (which must be sorted ascending by start). Used to score fallback
        /// candidates by how much of the character's script a face covers.
        /// </summary>
        /// <remarks>
        /// A table with both a format-4 and a format-12 subtable answers per subtable and the larger figure
        /// wins, rather than summing the two: the subtables usually overlap, and this is a relative score
        /// between faces, not a census of the font.
        /// </remarks>
        public static long OverlapWith(byte[] cmap, IReadOnlyList<RuneInterval> ranges)
        {
            var best = 0L;

            try
            {
                foreach (var (subtable, format) in LayoutOf(cmap).Subtables)
                {
                    if (format == 12)
                        best = Math.Max(best, OverlapFormat12(cmap, subtable, ranges));
                    else if (format == 4)
                        best = Math.Max(best, OverlapFormat4(cmap, subtable, ranges));
                }
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException or ArgumentException)
            {
            }

            return best;
        }

        /// <summary>
        /// Copies the <c>cmap</c> table out of a whole font's bytes - the form a custom/@font-face face is
        /// added in - or null when the directory has none or the font is a collection (whose faces the
        /// resolver registers individually).
        /// </summary>
        public static byte[]? ExtractTable(byte[] font)
        {
            try
            {
                if (font.Length < 12 || BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(0, 4)) == CollectionTag)
                    return null;

                int numTables = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4, 2));
                for (var i = 0; i < numTables; i++)
                {
                    int record = 12 + i * 16;
                    if (record + 16 > font.Length)
                        return null;

                    if (BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record, 4)) != CmapTag)
                        continue;

                    long offset = BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record + 8, 4));
                    long length = BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record + 12, 4));
                    if (offset < 0 || length <= 0 || offset + length > font.Length)
                        return null;

                    return font.AsSpan((int)offset, (int)length).ToArray();
                }
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException or ArgumentException)
            {
            }

            return null;
        }

        private static (int Offset, int Format)[] ReadSubtables(byte[] cmap)
        {
            try
            {
                if (cmap.Length < 4)
                    return [];

                int count = ReadU16(cmap, 2);
                var subtables = new List<(int, int)>(count);
                for (var i = 0; i < count; i++)
                {
                    int record = 4 + i * 8;
                    if (record + 8 > cmap.Length)
                        break;

                    long subtable = ReadU32(cmap, record + 4);
                    if (subtable < 0 || subtable + 2 > cmap.Length)
                        continue;

                    int format = ReadU16(cmap, (int)subtable);
                    if (format is 4 or 12)
                        subtables.Add(((int)subtable, format));
                }

                return [.. subtables];
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException or ArgumentException)
            {
                return [];
            }
        }

        /// <summary>Membership by the segment bounds, mirroring <see cref="CMapCoverage.Extract(CMap4?)"/>.</summary>
        private static bool CoversFormat4(byte[] cmap, int subtable, int codepoint)
        {
            // The terminator segment maps the U+FFFF noncharacter to .notdef and is not coverage.
            if (codepoint is > 0xFFFF or Format4Terminator)
                return false;

            var (segCount, endCodes, startCodes) = Format4Layout(cmap, subtable);
            if (segCount <= 0)
                return false;

            int low = 0, high = segCount - 1;
            while (low <= high)
            {
                int middle = (low + high) >> 1;
                int end = ReadU16(cmap, endCodes + middle * 2);

                if (end < codepoint)
                {
                    low = middle + 1;
                }
                else if (ReadU16(cmap, startCodes + middle * 2) <= codepoint)
                {
                    return true;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return false;
        }

        private static bool CoversFormat12(byte[] cmap, int subtable, int codepoint)
        {
            long numGroups = ReadU32(cmap, subtable + 12);
            long low = 0, high = numGroups - 1;
            int groups = subtable + 16;

            while (low <= high)
            {
                long middle = low + ((high - low) >> 1);
                long record = groups + middle * 12;
                if (record + 12 > cmap.Length)
                    return false;

                long start = ReadU32(cmap, (int)record);
                long end = ReadU32(cmap, (int)(record + 4));

                if (codepoint < start)
                    high = middle - 1;
                else if (codepoint > end)
                    low = middle + 1;
                else
                    return true;
            }

            return false;
        }

        private static long OverlapFormat4(byte[] cmap, int subtable, IReadOnlyList<RuneInterval> ranges)
        {
            var (segCount, endCodes, startCodes) = Format4Layout(cmap, subtable);
            if (segCount <= 0)
                return 0;

            long total = 0;
            for (var segment = 0; segment < segCount; segment++)
            {
                int start = ReadU16(cmap, startCodes + segment * 2);
                if (start == Format4Terminator)
                    continue;

                int end = Math.Min(ReadU16(cmap, endCodes + segment * 2), 0xFFFE);
                if (start > end)
                    continue;

                total += Intersection(start, end, ranges);
            }

            return total;
        }

        private static long OverlapFormat12(byte[] cmap, int subtable, IReadOnlyList<RuneInterval> ranges)
        {
            long numGroups = ReadU32(cmap, subtable + 12);
            int groups = subtable + 16;
            long total = 0;

            for (long group = 0; group < numGroups; group++)
            {
                long record = groups + group * 12;
                if (record + 12 > cmap.Length)
                    break;

                long start = ReadU32(cmap, (int)record);
                long end = ReadU32(cmap, (int)(record + 4));
                if (start > end || start > 0x10FFFF)
                    continue;

                total += Intersection((int)start, (int)Math.Min(end, 0x10FFFF), ranges);
            }

            return total;
        }

        /// <summary>How many code points of [<paramref name="start"/>, <paramref name="end"/>] the sorted <paramref name="ranges"/> hold.</summary>
        private static long Intersection(int start, int end, IReadOnlyList<RuneInterval> ranges)
        {
            long total = 0;

            foreach (var range in ranges)
            {
                var from = Math.Max(start, range.Start.Value);
                var to = Math.Min(end, range.End.Value);
                if (from <= to)
                    total += to - from + 1;

                if (range.Start.Value > end)
                    break;
            }

            return total;
        }

        private static (int SegCount, int EndCodes, int StartCodes) Format4Layout(byte[] cmap, int subtable)
        {
            if (subtable + 14 > cmap.Length)
                return (0, 0, 0);

            int segCount = ReadU16(cmap, subtable + 6) / 2;
            if (segCount == 0)
                return (0, 0, 0);

            int endCodes = subtable + 14;
            // The segment table: endCode[], reservedPad, startCode[] - see the format-4 layout.
            int startCodes = endCodes + segCount * 2 + 2;
            if (startCodes + segCount * 2 > cmap.Length)
                return (0, 0, 0);

            return (segCount, endCodes, startCodes);
        }

        private static int ReadU16(byte[] data, int offset) =>
            BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2));

        private static uint ReadU32(byte[] data, int offset) =>
            BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));
    }
}
