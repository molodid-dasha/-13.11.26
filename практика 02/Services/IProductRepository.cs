using molodid.Models;

namespace molodid.Services;

public interface IProductRepository
{
    bool Add(Product product);
    IReadOnlyList<Product> GetAll();
    Product? GetBySku(string sku);
    int Count { get; }
    void Clear();
}