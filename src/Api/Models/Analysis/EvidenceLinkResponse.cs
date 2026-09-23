using Core.Models;

namespace Api.Models.Analysis;

public sealed record EvidenceLinkResponse(string Url, string? Title, string Stance)
{
    public static EvidenceLinkResponse From(EvidenceLink link)
        => new(link.Url, link.Title, link.Stance.ToString());
}
