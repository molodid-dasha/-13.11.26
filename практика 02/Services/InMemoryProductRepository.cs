using molodid.Models;

namespace molodid.Services;

/// <summary>
/// Хранилище товаров в памяти. Регистрируется как Singleton.
/// </summary>
public class InMemoryProductRepository : IProductRepository
{
    private readonly List<Product> _products = new();
    private readonly object _lock = new();

    public int Count
    {
        get { lock (_lock) return _products.Count; }
    }

    // Добавляет товар. Возвращает false, если товар с таким артикулом уже есть (без учёта регистра).
    public bool Add(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);
        var key = NormalizeSku(product.Sku);

        lock (_lock)
        {
            if (_products.Any(p => string.Equals(NormalizeSku(p.Sku), key, StringComparison.OrdinalIgnoreCase)))
                return false;

            _products.Add(product);
            return true;
        }
    }

    public IReadOnlyList<Product> GetAll()
    {
        lock (_lock) return _products.ToList();
    }

    // Ищет товар по артикулу без учёта регистра. Возвращает null, если не найден.
    public Product? GetBySku(string sku)
    {
        var key = NormalizeSku(sku);
        lock (_lock)
        {
            return _products.FirstOrDefault(p => string.Equals(NormalizeSku(p.Sku), key, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void Clear()
    {
        lock (_lock) _products.Clear();
    }

    private static string NormalizeSku(string? sku)
        => (sku ?? string.Empty).Trim();
}