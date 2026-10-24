using System.Globalization;
using System.Text;

namespace Nudge.Api.Infrastructure;

public static class EmojiValidator
{
    // Counts what a person sees as characters. "👍🏽" is two C# chars (more, in fact) but one emoji,
    // so string.Length would be wrong here. StringInfo counts "text elements" instead.
    public static int CountEmojis(string value)
    {
        var count = 0;
        var e = StringInfo.GetTextElementEnumerator(value);
        while (e.MoveNext()) count++;
        return count;
    }

    // True if every visible character is an emoji-like symbol rather than a letter, digit or space.
    public static bool IsEmojiOnly(string value)
    {
        var e = StringInfo.GetTextElementEnumerator(value);
        while (e.MoveNext())
        {
            var element = (string)e.Current;
            var rune = element.EnumerateRunes().First();
            var category = Rune.GetUnicodeCategory(rune);
            var isSymbol = category is UnicodeCategory.OtherSymbol or UnicodeCategory.MathSymbol
                || rune.Value >= 0x1F000;   // the main emoji blocks
            if (!isSymbol) return false;
        }
        return true;
    }
}
