using CIRKEVNAHORESSR.Components;
using CIRKEVNAHORESSR.Services;
using Microsoft.AspNetCore.Localization;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");
builder.Services.AddScoped<ContactService>();
builder.Services.AddScoped<ResxLocationService>();

builder.Services.AddHttpContextAccessor();

// ✅ ČISTÉ SSR
builder.Services.AddRazorComponents();

var app = builder.Build();

// --------------------
// Localization options
// --------------------
var supportedCultures = new[]
{
    new CultureInfo("cs-CZ"),
    new CultureInfo("en-US"),
};

var loc = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("cs-CZ"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
};

// cookie provider (persist volby)
loc.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider());

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRequestLocalization(loc);

// --------------------
// Helpers
// --------------------
static string? GetLangFromCookie(HttpContext ctx)
{
    var v = ctx.Request.Cookies[CookieRequestCultureProvider.DefaultCookieName];
    if (string.IsNullOrWhiteSpace(v)) return null;

    if (v.Contains("en-US", StringComparison.OrdinalIgnoreCase)) return "en";
    if (v.Contains("cs-CZ", StringComparison.OrdinalIgnoreCase)) return "cs";
    return null;
}

// jednoduchá heuristika: nechceme přesměrovávat assety
static bool LooksLikeFilePath(string path)
{
    // /vendor/aos/aos.css, /img/x.png, /css/main.css, /js/main.js, ...
    // (a zároveň nechceme blokovat stránky s tečkou náhodou – ale to je u tebe ok)
    return path.Contains('.', StringComparison.OrdinalIgnoreCase);
}

static bool HasEnPrefix(string path)
    => path.Equals("/en", StringComparison.OrdinalIgnoreCase)
    || path.StartsWith("/en/", StringComparison.OrdinalIgnoreCase);

static string StripEnPrefix(string path)
{
    if (path.Equals("/en", StringComparison.OrdinalIgnoreCase)) return "/";
    if (path.StartsWith("/en/", StringComparison.OrdinalIgnoreCase)) return path.Substring(3);
    return path;
}

// --------------------
// Language persistence + URL normalization + routing strip
// --------------------
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "/";
    var qs = ctx.Request.QueryString.Value ?? "";

    // ✅ nikdy nesahej na /lang/... (jinak si rozbiješ endpointy)
    // + pojistka pro /en/lang/... (kdyby to někde vznikalo)
    if (path.StartsWith("/lang/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/en/lang/", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    var pref = GetLangFromCookie(ctx); // "en" / "cs" / null
    var enInUrl = HasEnPrefix(path);

    // ✅ Redirect podle cookie (jen pro "stránky", ne pro soubory)
    if (!LooksLikeFilePath(path))
    {
        if (pref == "en" && !enInUrl)
        {
            // /kontakt => /en/kontakt (routing pak namapuje na /contact přes MenuBase)
            var target = "/en" + (path == "/" ? "" : path);
            ctx.Response.Redirect(target + qs, permanent: false);
            return;
        }

        if (pref == "cs" && enInUrl)
        {
            // /en/contact => /contact
            var target = StripEnPrefix(path);
            ctx.Response.Redirect(target + qs, permanent: false);
            return;
        }
    }

    // ✅ Nastav culture primárně dle URL (/en má přednost), jinak dle cookie, jinak cs
    if (enInUrl)
    {
        var culture = new CultureInfo("en-US");
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        // ✅ pro routing stripni /en (jen pro stránky, ne pro soubory)
        if (!LooksLikeFilePath(path))
        {
            ctx.Request.Path = StripEnPrefix(path);
        }
    }
    else
    {
        var cultureName = pref == "en" ? "en-US" : "cs-CZ";
        var culture = new CultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    await next();
});

app.UseAntiforgery();

// --------------------
// Language switch endpoints
// --------------------
static IResult SetLangCookieAndRedirect(HttpContext ctx, string lang, string? returnUrl)
{
    lang = (lang ?? "").ToLowerInvariant();
    var culture = lang == "en" ? "en-US" : "cs-CZ";

    var cookieValue = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture));
    ctx.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        cookieValue,
        new CookieOptions
        {
            Path = "/",
            IsEssential = true,
            HttpOnly = false,
            SameSite = SameSiteMode.Lax,
            Secure = ctx.Request.IsHttps
        }
    );

    // bezpečný return url
    if (string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith("/"))
        returnUrl = lang == "en" ? "/en" : "/";

    return Results.Redirect(returnUrl);
}

// ✅ hlavní endpoint
app.MapGet("/lang/{lang}", (HttpContext ctx, string lang, string? returnUrl)
    => SetLangCookieAndRedirect(ctx, lang, returnUrl));

// ✅ pojistka: kdyby někde vzniklo /en/lang/...
app.MapGet("/en/lang/{lang}", (HttpContext ctx, string lang, string? returnUrl)
    => SetLangCookieAndRedirect(ctx, lang, returnUrl));

app.MapGet("/healthz", () => Results.Ok("ok"));

app.MapRazorComponents<App>();

app.Run();