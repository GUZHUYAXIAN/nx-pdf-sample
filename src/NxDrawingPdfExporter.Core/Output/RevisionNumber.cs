using System.Globalization;
using System.Linq;

namespace NxDrawingPdfExporter.Core.Output;

public static class RevisionNumber
{
    public static bool TryParse(string? text, out int value)
    {
        value = 0;
        var digits = text?.Trim() ?? "";
        if (digits.Length > 0 && (digits[0] == 'R' || digits[0] == 'r'))
            digits = digits.Substring(1);
        return digits.Length > 0 && digits.All(c => c >= '0' && c <= '9')
            && int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
}
