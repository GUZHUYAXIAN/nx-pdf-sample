using System.Globalization;
using System.Resources;
using NxDrawingPdfExporter.Contracts;

namespace NxDrawingPdfExporter.App.Localization;

public sealed class UiText
{
    private static readonly ResourceManager Resources = new("NxDrawingPdfExporter.App.Localization.Strings", typeof(UiText).Assembly);
    private string language = "zh-CN";
    public event Action? Changed;

    public string Language
    {
        get => language;
        set
        {
            var normalized = value == "en" ? "en" : "zh-CN";
            if (language == normalized)
                return;
            language = normalized;
            Changed?.Invoke();
        }
    }

    public string Get(string key, params object[] arguments)
    {
        var culture = CultureInfo.GetCultureInfo(Language);
        var value = Resources.GetString(key, culture) ?? key;
        return arguments.Length == 0 ? value : string.Format(culture, value, arguments);
    }

    public string Reason(string reasonCode, IReadOnlyList<string>? arguments = null)
    {
        var key = "Reason." + reasonCode;
        var message = Get(key);
        if (message == key)
            message = Get("Reason." + ResultReasons.Unknown);
        return arguments is { Count: > 0 } && arguments.Any(arg => !string.IsNullOrWhiteSpace(arg))
            ? message + Environment.NewLine + string.Join(Environment.NewLine, arguments) : message;
    }
}
