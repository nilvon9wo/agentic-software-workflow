using System;

namespace WordCounter
{
    /// <summary>
    /// Provides word counting utilities where a word is defined as a maximal
    /// contiguous run of Unicode letters or digits (per char.IsLetterOrDigit).
    /// </summary>
    public static class WordCounter
    {
        /// <summary>
        /// Counts the number of words in the input string, where a word is a
        /// maximal contiguous sequence of characters for which
        /// char.IsLetterOrDigit returns true.
        /// </summary>
        /// <param name="input">The input string to scan.</param>
        /// <returns>The number of words found.</returns>
        /// <exception cref="ArgumentNullException">Thrown when input is null.</exception>
        public static int CountWords(string input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            int count = 0;
            bool inWord = false;

            foreach (char c in input)
            {
                if (char.IsLetterOrDigit(c))
                {
                    if (!inWord)
                    {
                        count++;
                        inWord = true;
                    }
                }
                else
                {
                    inWord = false;
                }
            }

            return count;
        }
    }
}
