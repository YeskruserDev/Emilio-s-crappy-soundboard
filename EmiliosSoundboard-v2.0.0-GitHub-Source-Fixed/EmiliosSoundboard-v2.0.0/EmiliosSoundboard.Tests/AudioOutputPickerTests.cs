using System.Collections.Generic;
using Xunit;

namespace EmiliosSoundboard.Tests
{
    public class AudioOutputPickerTests
    {
        private static List<string> Devices(params string[] names)
        {
            return new List<string>(names);
        }

        [Fact]
        public void ResolveIndex_FindsAnExactName()
        {
            var names = Devices("Speakers (Realtek Audio)", "CABLE Input (VB-Audio Virtual Cable)");

            Assert.Equal(1, AudioOutputPicker.ResolveIndex("CABLE Input (VB-Audio Virtual Cable)", names));
            Assert.Equal(0, AudioOutputPicker.ResolveIndex("Speakers (Realtek Audio)", names));
        }

        [Fact]
        public void ResolveIndex_IgnoresCaseAndSurroundingSpaces()
        {
            var names = Devices("Speakers (Realtek Audio)");

            Assert.Equal(0, AudioOutputPicker.ResolveIndex("  speakers (REALTEK audio)  ", names));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("System default")]
        [InlineData("system DEFAULT")]
        [InlineData("Same as the board")]
        public void ResolveIndex_TreatsUnsetAndDefaultAsTheSystemDefault(string stored)
        {
            var names = Devices("Speakers", "CABLE Input");

            Assert.Equal(AudioOutputPicker.DefaultIndex, AudioOutputPicker.ResolveIndex(stored, names));
        }

        [Fact]
        public void ResolveIndex_FallsBackToTheDefaultWhenTheDeviceIsGone()
        {
            var names = Devices("Speakers (Realtek Audio)");

            Assert.Equal(AudioOutputPicker.DefaultIndex, AudioOutputPicker.ResolveIndex("CABLE Input (VB-Audio)", names));
        }

        [Fact]
        public void ResolveIndex_HandlesAnEmptyDeviceList()
        {
            Assert.Equal(AudioOutputPicker.DefaultIndex, AudioOutputPicker.ResolveIndex("Speakers", new List<string>()));
            Assert.Equal(AudioOutputPicker.DefaultIndex, AudioOutputPicker.ResolveIndex("Speakers", null));
        }

        [Fact]
        public void ResolveIndex_AcceptsATruncatedNameWhenItIsUnambiguous()
        {
            // winmm truncates names, and a driver update can append a suffix.
            var names = Devices("CABLE Input (VB-Audio Virtual Cable)", "Speakers (Realtek Audio)");

            Assert.Equal(0, AudioOutputPicker.ResolveIndex("CABLE Input", names));
        }

        [Fact]
        public void ResolveIndex_RefusesAnAmbiguousPartialMatch()
        {
            var names = Devices("Speakers (Realtek Audio)", "Speakers (USB Headset)");

            Assert.Equal(AudioOutputPicker.DefaultIndex, AudioOutputPicker.ResolveIndex("Speakers", names));
        }

        [Fact]
        public void Options_StartsWithTheDefaultAndSkipsDuplicatesAndBlanks()
        {
            var names = Devices("Speakers", "", "  ", "CABLE Input", "speakers");

            var options = AudioOutputPicker.Options(names);

            Assert.Equal(AudioOutputPicker.SystemDefault, options[0]);
            Assert.Equal(3, options.Count);           // default, Speakers, CABLE Input
            Assert.Contains("Speakers", options);
            Assert.Contains("CABLE Input", options);
        }

        [Fact]
        public void Options_StillWorksWithoutDevices()
        {
            var options = AudioOutputPicker.Options(null);

            Assert.Single(options);
            Assert.Equal(AudioOutputPicker.SystemDefault, options[0]);
        }

        [Fact]
        public void Describe_PrefersTheResolvedNameAndFallsBackToWhatWasStored()
        {
            var names = Devices("Speakers", "CABLE Input");

            Assert.Equal("CABLE Input", AudioOutputPicker.Describe("whatever", 1, names));
            Assert.Equal(AudioOutputPicker.SystemDefault, AudioOutputPicker.Describe(null, -1, names));
            Assert.Equal("Gone Device", AudioOutputPicker.Describe("Gone Device", -1, names));
        }
    }
}
