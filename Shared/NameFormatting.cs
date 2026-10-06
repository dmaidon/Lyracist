// Edited on Oct 6, 2026 @ 10:46:00 -> Add CleanSingerName to strip placeholder text and sanitize singer names
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Lyracist.Shared
{
    /// <summary>
    /// Provides intelligent proper-casing and capitalization for singer names, artist names,
    /// and song titles across all applications in the solution.
    /// </summary>
    public static class NameFormatting
    {
        private static readonly HashSet<string> AcronymsAndNumerals = new(StringComparer.OrdinalIgnoreCase)
        {
            "DJ", "MC", "TV", "CD", "DVD", "EP", "LP", "UK", "USA", "US",
            "II", "III", "IV", "VI", "VII", "VIII", "IX", "X", "XI", "XII", "XIII", "XIV", "XV"
        };

        /// <summary>
        /// Cleans a singer name by removing any leftover placeholder text such as "New Singer".
        /// If the string contains only "New Singer" (or is whitespace), returns <see cref="string.Empty"/>.
        /// If "New Singer" appears alongside other text (e.g. "New Singertom" or "New Singer Tom"),
        /// strips "New Singer" out and returns the properly cased name (e.g. "Tom").
        /// </summary>
        public static string CleanSingerName(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            string trimmed = input.Trim();

            if (trimmed.Equals("New Singer", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            // Remove any occurrence of "New Singer" (case-insensitive, requiring the single space between words)
            string stripped = Regex.Replace(trimmed, @"(?i)\bnew singer", string.Empty);

            // Clean up any leftover punctuation or whitespace (e.g. leading/trailing dashes, colons)
            stripped = stripped.Trim(' ', '-', ':', ',', '.');

            if (string.IsNullOrWhiteSpace(stripped))
                return string.Empty;

            return ProperCase(stripped);
        }

        /// <summary>
        /// Converts the given input string to proper case while preserving intentional mixed-case
        /// capitalizations (e.g. "DeaR", "McDonald", "LeBron"), capitalizing prefix names with
        /// apostrophes (e.g. "O'Neal", "D'Angelo", "L'Amour"), handling "Mc" prefixes ("McDonald"),
        /// and formatting hyphenated names and contractions correctly.
        /// </summary>
        /// <param name="input">The raw text to format.</param>
        /// <returns>The properly capitalized string.</returns>
        public static string ProperCase(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input ?? string.Empty;
            if (input.Trim().Equals("None", StringComparison.OrdinalIgnoreCase)) return "None";

            var sb = new StringBuilder(input.Length);
            var wordBuffer = new StringBuilder();

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (IsWordDelimiter(c))
                {
                    if (wordBuffer.Length > 0)
                    {
                        sb.Append(FormatWord(wordBuffer.ToString()));
                        wordBuffer.Clear();
                    }
                    sb.Append(c);
                }
                else
                {
                    wordBuffer.Append(c);
                }
            }

            if (wordBuffer.Length > 0)
            {
                sb.Append(FormatWord(wordBuffer.ToString()));
            }

            return sb.ToString();
        }

        private static bool IsWordDelimiter(char c)
        {
            return char.IsWhiteSpace(c)
                || c is '-' or '–' or '—'
                || c is '(' or ')' or '[' or ']' or '{' or '}' or '<' or '>'
                || c is '/' or '\\' or '|'
                || c is '"' or '“' or '”'
                || c is ',' or ';' or ':' or '!' or '?' or '*' or '+' or '&' or '=' or '_' or '~';
        }

        private static string FormatWord(string word)
        {
            if (string.IsNullOrEmpty(word)) return word;

            // Handle enclosing single quotes like 'song' or 'N'
            if (word.Length >= 2 && (word.StartsWith('\'') || word.StartsWith('’')) && (word.EndsWith('\'') || word.EndsWith('’')))
            {
                char openQuote = word[0];
                char closeQuote = word[^1];
                string inner = word.Substring(1, word.Length - 2);
                if (inner.Equals("n", StringComparison.OrdinalIgnoreCase))
                {
                    return $"{openQuote}N{closeQuote}";
                }
                return $"{openQuote}{FormatWord(inner)}{closeQuote}";
            }

            if (word.StartsWith('\'') || word.StartsWith('’'))
            {
                char openQuote = word[0];
                string inner = word.Substring(1);
                if (inner.Equals("n'", StringComparison.OrdinalIgnoreCase) || inner.Equals("n’", StringComparison.OrdinalIgnoreCase))
                {
                    return $"{openQuote}N{inner[^1]}";
                }
                if (inner.Equals("n", StringComparison.OrdinalIgnoreCase))
                {
                    return $"{openQuote}N";
                }
                return $"{openQuote}{FormatWord(inner)}";
            }

            if (word.EndsWith('\'') || word.EndsWith('’'))
            {
                char closeQuote = word[^1];
                string inner = word.Substring(0, word.Length - 1);
                return $"{FormatWord(inner)}{closeQuote}";
            }

            if (word.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                return "None";
            }

            if (AcronymsAndNumerals.Contains(word))
            {
                return word.ToUpperInvariant();
            }

            // Check for single-letter apostrophe prefixes (e.g. O'Neal, D'Angelo, L'Amour, M'Baku)
            int apostropheIdx = word.IndexOfAny(['\'', '’']);
            if (apostropheIdx == 1 && char.IsLetter(word[0]) && word.Length > 2)
            {
                char prefix = char.ToUpper(word[0]);
                char ap = word[1];
                string rest = word.Substring(2);
                return $"{prefix}{ap}{FormatWord(rest)}";
            }
            else if (apostropheIdx > 1)
            {
                // Contractions or possessives (e.g. don't, can't, it's, john's)
                bool hasLower = false;
                bool hasUpper = false;
                for (int k = 0; k < word.Length; k++)
                {
                    if (char.IsLower(word[k])) hasLower = true;
                    if (char.IsUpper(word[k])) hasUpper = true;
                }

                if (hasUpper && !hasLower)
                {
                    // All-caps contraction: DON'T -> Don't, JOHN'S -> John's
                    string before = word.Substring(0, apostropheIdx);
                    char ap = word[apostropheIdx];
                    string after = word.Substring(apostropheIdx + 1);
                    return $"{FormatWord(before)}{ap}{after.ToLowerInvariant()}";
                }
                else if (hasUpper && hasLower)
                {
                    // Mixed case (e.g. Don'T or custom apostrophe casing)
                    char[] chars = word.ToCharArray();
                    chars[0] = char.ToUpper(chars[0]);
                    return new string(chars);
                }
                else
                {
                    // All lowercase contraction: don't -> Don't, can't -> Can't
                    char[] chars = word.ToCharArray();
                    chars[0] = char.ToUpper(chars[0]);
                    return new string(chars);
                }
            }

            // Check for Mc prefix: McDonald, McCartney
            if (word.Length >= 3 && word.StartsWith("mc", StringComparison.OrdinalIgnoreCase) && char.IsLetter(word[2]))
            {
                string rest = word.Substring(2);
                return $"Mc{FormatWord(rest)}";
            }

            // Standard word casing:
            int upperCount = 0;
            int lowerCount = 0;
            for (int k = 0; k < word.Length; k++)
            {
                if (char.IsUpper(word[k])) upperCount++;
                else if (char.IsLower(word[k])) lowerCount++;
            }

            if (upperCount > 0 && lowerCount > 0)
            {
                // Mixed case (e.g. DeaR, LeBron, MacBook, iPad, MacDonald)
                // Capitalize the first letter, but preserve all other user-typed capitalizations
                char[] chars = word.ToCharArray();
                chars[0] = char.ToUpper(chars[0]);
                return new string(chars);
            }
            else if (upperCount > 0 && lowerCount == 0)
            {
                // All uppercase (e.g. DENNIS, MAIDON, DEAR)
                // Capitalize first letter, lowercase the rest
                char[] chars = word.ToCharArray();
                chars[0] = char.ToUpper(chars[0]);
                for (int k = 1; k < chars.Length; k++)
                {
                    chars[k] = char.ToLower(chars[k]);
                }
                return new string(chars);
            }
            else
            {
                // All lowercase (e.g. dennis, maidon, dear)
                // Capitalize first letter
                char[] chars = word.ToCharArray();
                chars[0] = char.ToUpper(chars[0]);
                return new string(chars);
            }
        }
    }
}
