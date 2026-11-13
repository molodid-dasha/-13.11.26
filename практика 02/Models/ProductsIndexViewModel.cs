using System.Collections.Generic;

namespace molodid.Models;

public class ProductsIndexViewModel
{
    public IReadOnlyList<Product> Products { get; set; } = new List<Product>();
    public int TotalCount => Products.Count;
    public decimal TotalValue => Products.Sum(p => p.Price * p.StockQuantity);

    // Для поиска и фильтрации
    public string SearchString { get; set; } = string.Empty;
    public string SelectedCategory { get; set; } = string.Empty;
    public string SortOrder { get; set; } = string.Empty;
    public List<string> Categories { get; set; } = new List<string>();
}