namespace Infrastructure.Services;

public sealed class MlServiceOptions
{
    public const string SectionName = "MlService";

    /// <summary>Named client shared by the visualizer and every detector.</summary>
    public const string HttpClientName = "MlService";

    public Uri BaseUrl { get; set; } = new("http://localhost:8000/");

    /// <summary>Covers the upload plus inference; first calls can be slow while a model warms up.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Detector ids to call, one <c>POST /detect/{id}</c> each (see <c>GET /health</c> on the service).
    /// Set in appsettings rather than defaulted here, because the configuration binder appends
    /// to a pre-filled list instead of replacing it.
    /// </summary>
    public List<string> Detectors { get; set; } = [];
}
