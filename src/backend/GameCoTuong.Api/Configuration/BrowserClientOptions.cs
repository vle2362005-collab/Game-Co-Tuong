namespace GameCoTuong.Api.Configuration;

public sealed class BrowserClientOptions
{
    public const string SectionName = "BrowserClient";
    public string[] AllowedOrigins { get; init; } = [];

    public bool HasValidOrigins() => AllowedOrigins.Length > 0 && AllowedOrigins.All(origin =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && string.IsNullOrEmpty(uri.UserInfo)
        && origin == uri.GetLeftPart(UriPartial.Authority));
}
