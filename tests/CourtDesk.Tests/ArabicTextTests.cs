using CourtDesk.Domain;
using Xunit;

namespace CourtDesk.Tests
{
    [Trait("Category", "Domain")]
    public sealed class ArabicTextTests
    {
        [Fact]
        public void StripsTashkeelAndTatweel()
        {
            Assert.Equal("محمد", ArabicText.Normalize("مُحَمَّد"));
            Assert.Equal("محمد", ArabicText.Normalize("محمـد"));
        }

        [Fact]
        public void MapsAlefYaMarbuta()
        {
            Assert.Equal("احمد ابراهيم ادم", ArabicText.Normalize("أحمد إبراهيم آدم"));
            Assert.Equal("مصطفي", ArabicText.Normalize("مصطفى"));
            Assert.Equal("فاطمه", ArabicText.Normalize("فاطمة"));
            Assert.Equal("مومن", ArabicText.Normalize("مؤمن"));
            Assert.Equal("قاري", ArabicText.Normalize("قارئ"));
        }

        [Fact]
        public void MapsBothDigitSets()
        {
            Assert.Equal("123", ArabicText.Normalize("١٢٣"));
            Assert.Equal("123", ArabicText.Normalize("۱۲۳"));
            Assert.Equal("12 قضيه 34", ArabicText.Normalize("12 قضية 34"));
        }

        [Fact]
        public void RemovesBidiControls()
        {
            Assert.Equal("محمدعلي", ArabicText.Normalize("محمد‌علي"));
            Assert.Equal("محمد", ArabicText.Normalize("‫محمد‬"));
        }

        [Fact]
        public void CollapsesWhitespaceAndTrims()
        {
            Assert.Equal("احمد علي", ArabicText.Normalize("  أحمد   علي  "));
        }

        [Fact]
        public void LowercasesLatin()
        {
            Assert.Equal("abc", ArabicText.Normalize("ABC"));
        }

        [Fact]
        public void JoinedFindsSpacedAndUnspaced()
        {
            Assert.Equal(ArabicText.Joined("عبد الله"), ArabicText.Joined("عبدالله"));
            Assert.Equal("عبدالله", ArabicText.Joined("عبد الله"));
        }

        [Fact]
        public void EmptyStaysEmpty()
        {
            Assert.Equal("", ArabicText.Normalize(""));
            Assert.Equal("", ArabicText.Normalize(null));
            Assert.Equal("", ArabicText.Joined(null));
        }
    }
}
