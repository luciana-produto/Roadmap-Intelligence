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
            Name = name.Trim(),
            Slug = slug.Trim().ToLowerInvariant()
        };

    public void Update(string name, string slug)
    {
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
    }

    public RoadmapProduct AddProduct(string name)
    {
        var product = RoadmapProduct.Create(name.Trim(), Id);
        _products.Add(product);
        return product;
    }

    public RoadmapProduct UpdateProduct(Guid productId, string name)
    {
        var product = _products.First(p => p.Id == productId);
        product.Update(name.Trim());
        return product;
    }

    public void RemoveProduct(Guid productId)
    {
        var product = _products.First(p => p.Id == productId);
        _products.Remove(product);
    }
}
