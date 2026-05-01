namespace ProductHub.Domain.Roadmap;

public sealed class RoadmapIssueLink
{
    public string Key { get; private set; } = string.Empty;
    public string Url { get; private set; } = string.Empty;

    private RoadmapIssueLink() { }

    public static RoadmapIssueLink Create(string key, string url)
    {
        var normalizedKey = key.Trim();
        var normalizedUrl = url.Trim();

        if (string.IsNullOrWhiteSpace(normalizedKey))
            throw new ArgumentException("Issue key is required.", nameof(key));

        if (string.IsNullOrWhiteSpace(normalizedUrl))
            throw new ArgumentException("Issue URL is required.", nameof(url));

        return new RoadmapIssueLink
        {
            Key = normalizedKey,
            Url = normalizedUrl
        };
    }
}