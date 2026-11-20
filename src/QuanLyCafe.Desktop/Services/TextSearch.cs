using System.Globalization;
using System.Text;

namespace QuanLyCafe.Desktop.Services;

public static class TextSearch
{
    public static bool Matches(string text, string query) =>
        Fold(text).Contains(Fold(query), StringComparison.OrdinalIgnoreCase);

    private static string Fold(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c == 'đ' ? 'd' : c == 'Đ' ? 'D' : c);
        return sb.ToString().Trim();
    }
}
