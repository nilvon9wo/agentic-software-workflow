using System;
using Xunit;

namespace WordCounterHiddenTests
{
    // Deeper normative checks that go beyond the illustrative examples in the
    // specification: Unicode-aware classification across scripts and digit
    // systems, separator-run collapsing, non-letter/digit punctuation such as
    // underscore and apostrophe, and order-independence of letter/digit runs.
    public class WordCounterNormativeBehaviorTests
    {
        [Fact]
        public void CountWords_NullInput_ThrowsArgumentNullExceptionSpecifically()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => WordCounter.WordCounter.CountWords(null!));
            Assert.IsType<ArgumentNullException>(ex);
        }

        [Theory]
        // Underscore is not a letter or digit (Unicode category Pc), so it is a separator.
        [InlineData("foo_bar", 2)]
        // Apostrophe is not a letter or digit, so it splits contractions into two words,
        // per the specification's stated definition with no special-casing.
        [InlineData("don't", 2)]
        // A digit immediately followed by a letter (no separator) forms a single maximal run,
        // just as a letter followed by a digit does ("abc123" -> 1).
        [InlineData("3D", 1)]
        // A run of multiple different separator characters collapses to a single boundary.
        [InlineData("foo,,,,bar", 2)]
        [InlineData("hello---world!!!foo", 3)]
        // Leading and trailing punctuation around a single letter run still yields one word.
        [InlineData(",word,", 1)]
        // Tab-only input is whitespace-only input -> 0 words.
        [InlineData("\t\t", 0)]
        // A single separator character on its own yields 0 words.
        [InlineData("!", 0)]
        // A mixture of whitespace and punctuation separators with no letters/digits -> 0.
        [InlineData(" \t\n!!!,,, ", 0)]
        // Cyrillic letters are letters under Unicode-aware classification.
        [InlineData("\u043F\u0440\u0438\u0432\u0435\u0442 \u043C\u0438\u0440", 2)]
        // CJK ideographs are individually classified as letters (category Lo); with no
        // separators between them, a run of consecutive CJK characters is one maximal word.
        [InlineData("\u4F60\u597D\u4E16\u754C", 1)]
        // Arabic-Indic digits (category Nd) are digits under Unicode-aware classification,
        // so a contiguous run of them forms a single word.
        [InlineData("\u0661\u0662\u0663", 1)]
        public void CountWords_ReturnsExpectedCount_ForNormativeEdgeCases(string input, int expected)
        {
            int actual = WordCounter.WordCounter.CountWords(input);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void CountWords_MixedScriptWord_IsCountedAsSingleContiguousWord()
        {
            // A run mixing Latin letters, an accented letter, and digits with no separators
            // is still a single maximal run under the letter-or-digit definition.
            int actual = WordCounter.WordCounter.CountWords("na\u00EFve42");
            Assert.Equal(1, actual);
        }

        [Fact]
        public void CountWords_MultipleAccentedWordsSeparatedByPunctuation_CountsEachWord()
        {
            int actual = WordCounter.WordCounter.CountWords("caf\u00E9, na\u00EFve, r\u00E9sum\u00E9");
            Assert.Equal(3, actual);
        }
    }
}
