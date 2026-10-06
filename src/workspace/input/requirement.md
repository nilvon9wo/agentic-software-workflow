# Word Counter

Build a .NET 10 class library named WordCounter.

It must expose a public static class `WordCounter.WordCounter` with a public
static method `CountWords(string input)` returning an integer.

A word is a maximal contiguous sequence of letters or digits. Punctuation,
whitespace, and other non-alphanumeric characters separate words and are not
themselves part of a word.

Null input is invalid and must throw `ArgumentNullException`.
Empty or whitespace-only input contains zero words.