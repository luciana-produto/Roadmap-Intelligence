using ProductHub.Domain.Common;

namespace ProductHub.Domain.Roadmap;

public sealed class RoadmapProject : AggregateRoot
{
    private List<RoadmapProduct> _products = [];

    public string Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public IReadOnlyList<RoadmapProduct> Products => _products.AsReadOnly();

    private RoadmapProject() { }

    public static RoadmapProject Create(string name, string slug) =>
        new()
        {
            Name = name,
            Slug = slug.ToLowerInvariant()
        };

    public RoadmapProduct AddProduct(string name)
    {
        var product = RoadmapProduct.Create(name, Id);
        _products.Add(product);
        return product;
    }
}
