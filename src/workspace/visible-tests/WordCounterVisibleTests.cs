using System;
using Xunit;

namespace WordCounterVisibleTests
{
    // Tests derived directly from the acceptance criteria of the specification.
    // A word is defined as a maximal contiguous run of Unicode letters/digits
    // (per char.IsLetterOrDigit). Everything else is a separator.
    public class WordCounterPublicApiTests
    {
        [Fact]
        public void CountWords_NullInput_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => WordCounter.WordCounter.CountWords(null!));
        }

        [Theory]
        [InlineData("", 0)]
        [InlineData("   ", 0)]
        [InlineData("!!!", 0)]
        [InlineData("hello", 1)]
        [InlineData("hello world", 2)]
        [InlineData("hello, world!", 2)]
        [InlineData("abc123", 1)]
        [InlineData("abc-123", 2)]
        [InlineData("  leading and trailing  ", 3)]
        [InlineData("word1\nword2\tword3", 3)]
        [InlineData("a", 1)]
        [InlineData("123", 1)]
        [InlineData("caf\u00E9", 1)]
        [InlineData("na\u00EFve???42", 2)]
        public void CountWords_ReturnsExpectedCount(string input, int expected)
        {
            int actual = WordCounter.WordCounter.CountWords(input);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void CountWords_IsAccessibleAsPublicStaticMemberFromExternalAssembly()
        {
            // The mere fact this compiles and runs from a separate test assembly
            // demonstrates WordCounter.WordCounter.CountWords is public and static.
            int result = WordCounter.WordCounter.CountWords("public api check");
            Assert.Equal(3, result);
        }
    }
}
