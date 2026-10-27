using PeachBrackets.Html.Paint;
using PeachDrawing.Text.Shaping;
using Xunit;

namespace PeachPDF.Tests.Integration
{
    /// <summary>
    /// <see cref="FragmentPainter.UprightVerticalShapingFeatures"/> - the <c>vert</c> request CSS Writing
    /// Modes requires for an upright run in a vertical writing mode (css-writing-modes-4 §5.1). No bundled
    /// font has to carry the feature for the merge itself to be checkable.
    /// </summary>
    public class VerticalShapingFeaturesTests
    {
        [Fact]
        public void AddsVertToSettingsThatDoNotMentionIt()
        {
            var features = FragmentPainter.UprightVerticalShapingFeatures(null);

            Assert.NotNull(features.ExplicitFeatures);
            Assert.Contains(features.ExplicitFeatures!, f => f.Tag == "vert" && f.Value == 1);
        }

        [Fact]
        public void PreservesOtherExplicitFeatures()
        {
            var settings = new ShapeSettings(ExplicitFeatures: [new FeatureSetting("ss01", 2)]);

            var features = FragmentPainter.UprightVerticalShapingFeatures(settings);

            Assert.Contains(features.ExplicitFeatures!, f => f.Tag == "ss01" && f.Value == 2);
            Assert.Contains(features.ExplicitFeatures!, f => f.Tag == "vert" && f.Value == 1);
        }

        [Fact]
        public void LeavesAnAuthoredVertRequestAlone()
        {
            // CSS font-feature-settings can switch the UA's vert off again; adding a second "vert 1" after
            // an authored "vert 0" would make the last one win and defeat that.
            var settings = new ShapeSettings(ExplicitFeatures: [new FeatureSetting("vert", 0)]);

            var features = FragmentPainter.UprightVerticalShapingFeatures(settings);

            Assert.Same(settings.ExplicitFeatures, features.ExplicitFeatures);
        }

        [Fact]
        public void ReusesTheSameSettingsForTheSameInput()
        {
            var settings = new ShapeSettings(ExplicitFeatures: [new FeatureSetting("ss01", 1)]);

            var first = FragmentPainter.UprightVerticalShapingFeatures(settings);
            var second = FragmentPainter.UprightVerticalShapingFeatures(settings);

            // The shaper's GSUB planning cache keys on the whole settings value, whose feature lists
            // compare by reference - a fresh list per word would recompute the active lookups every time.
            Assert.Same(first.ExplicitFeatures, second.ExplicitFeatures);
        }
    }
}
