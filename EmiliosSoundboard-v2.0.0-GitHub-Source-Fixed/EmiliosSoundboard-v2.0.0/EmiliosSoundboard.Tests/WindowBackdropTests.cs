using Xunit;

namespace EmiliosSoundboard.Tests
{
    public class WindowBackdropTests
    {
        [Theory]
        [InlineData("Mica", BackdropKind.Mica)]
        [InlineData("mica", BackdropKind.Mica)]
        [InlineData("  MICA  ", BackdropKind.Mica)]
        [InlineData("Acrylic", BackdropKind.Acrylic)]
        [InlineData("acrylic", BackdropKind.Acrylic)]
        [InlineData("Off", BackdropKind.Off)]
        [InlineData("off", BackdropKind.Off)]
        [InlineData("None", BackdropKind.Off)]
        public void Parse_ReadsEveryStoredSpelling(string stored, BackdropKind expected)
        {
            Assert.Equal(expected, WindowBackdrop.Parse(stored));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("garbage")]
        public void Parse_FallsBackToTheDefaultForUnknownValues(string stored)
        {
            Assert.Equal(BackdropKind.Mica, WindowBackdrop.Parse(stored));
        }

        [Theory]
        [InlineData(BackdropKind.Mica)]
        [InlineData(BackdropKind.Acrylic)]
        [InlineData(BackdropKind.Off)]
        public void Describe_Parse_RoundTrips(BackdropKind kind)
        {
            string stored = WindowBackdrop.Describe(kind);

            Assert.Equal(kind, WindowBackdrop.Parse(stored));
        }

        [Fact]
        public void Default_IsMicaAndMatchesTheSettingsDefault()
        {
            Assert.Equal("Mica", WindowBackdrop.Default);
            Assert.Equal(WindowBackdrop.Default, new AppSettings().WindowEffect);
        }
    }
}
