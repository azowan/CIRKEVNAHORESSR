using Microsoft.AspNetCore.Components;
using System.Globalization;

namespace DANIELKRALIKSSR.Components.Pages.Global
{
    public class MenuBase : ComponentBase
    {
        [Inject] protected NavigationManager Nav { get; set; } = default!;

        protected static readonly Dictionary<string, string> CsToEn = new()
        {
            ["/"] = "/en",
            ["/informace"] = "/en/information",
            ["/zivotopis"] = "/en/resume",
            ["/portfolio"] = "/en/portfolio",
            ["/sluzby"] = "/en/services",
            ["/kontakt"] = "/en/contact",
        };

        private static readonly Dictionary<string, string> EnToCs =
            CsToEn.ToDictionary(kv => kv.Value, kv => kv.Key);

        // ✅ spolehlivé v SSR
        protected bool IsEnglish =>
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase);

        protected string CurrentFlag => IsEnglish ? "/img/flags/en.png" : "/img/flags/cz.png";

        protected string LocalizePath(string csPath)
        {
            var normalized = (csPath == "/" ? "/" : csPath.TrimEnd('/')).ToLowerInvariant();
            if (IsEnglish && CsToEn.TryGetValue(normalized, out var en))
                return en;
            return normalized;
        }

        protected string isActive(string targetCsPath)
        {
            var rel = "/" + Nav.ToBaseRelativePath(Nav.Uri).TrimEnd('/');
            if (rel == "//") rel = "/";
            // když jsi v EN, URL je /en/..., active porovnávej na LocalizePath
            return rel.Equals(LocalizePath(targetCsPath), StringComparison.OrdinalIgnoreCase) ? "active" : "";
        }

        protected string SwitchTo(string lang)
{
    var rel = "/" + Nav.ToBaseRelativePath(Nav.Uri);
    if (!rel.StartsWith("/")) rel = "/" + rel;

    string returnUrl;
    if (lang == "en")
    {
        var cs = StripEn(rel);
        cs = cs == "" ? "/" : cs;
        returnUrl = CsToEn.TryGetValue(Norm(cs), out var en) ? en : "/en";
    }
    else
    {
        var en = EnsureEn(rel);
        returnUrl = EnToCs.TryGetValue(Norm(en), out var cs) ? cs : "/";
    }

    // ✅ absolutní (včetně domény), nikdy to neskončí jako /en/lang/...
    var baseUri = Nav.BaseUri.TrimEnd('/');
    return $"{baseUri}/lang/{lang}?returnUrl={Uri.EscapeDataString(returnUrl)}";
}

        private static string Norm(string p)
        {
            if (string.IsNullOrWhiteSpace(p)) return "/";
            p = p.Split('?', '#')[0];
            if (!p.StartsWith("/")) p = "/" + p;
            return p.Length > 1 ? p.TrimEnd('/') : p;
        }

        private static string StripEn(string path)
        {
            var p = Norm(path);
            if (p.Equals("/en", StringComparison.OrdinalIgnoreCase)) return "/";
            if (p.StartsWith("/en/", StringComparison.OrdinalIgnoreCase)) return p.Substring(3);
            return p;
        }

        private static string EnsureEn(string path)
        {
            var p = Norm(path);
            if (p.Equals("/", StringComparison.OrdinalIgnoreCase)) return "/en";
            return p.StartsWith("/en", StringComparison.OrdinalIgnoreCase) ? p : "/en" + p;
        }
    }
}
