using molodid.Models;
using molodid.Services;

namespace molodid.Tests;

public class RepositoryTests
{
    private static Product CreateProduct(string sku = "1001") => new()
    {
        Sku = sku,
        Name = "Ноутбук Lenovo IdeaPad 3",
        Price = 45990m,
        Brand = "Lenovo",
        WarrantyMonths = 24,
        ModelNumber = "IdeaPad3-15"
    };

    [Fact]
    public void Add_NewProduct_ReturnsTrueAndGetAllContainsIt()
    {
        var repository = new InMemoryProductRepository();
        var product = CreateProduct();

        var added = repository.Add(product);

        Assert.True(added);
        Assert.Equal(1, repository.Count);
        Assert.Contains(product, repository.GetAll());
    }

    [Fact]
    public void GetBySku_ExistingSku_ReturnsProduct()
    {
        var repository = new InMemoryProductRepository();
        var product = CreateProduct();
        repository.Add(product);

        var found = repository.GetBySku("1001");

        Assert.Same(product, found);
    }

    [Fact]
    public void GetBySku_SkuWithSpaces_ReturnsProduct()
    {
        var repository = new InMemoryProductRepository();
        var product = CreateProduct();
        repository.Add(product);

        var found = repository.GetBySku(" 1001 ");

        Assert.Same(product, found);
    }

    [Fact]
    public void GetBySku_DifferentCase_ReturnsProduct()
    {
        var repository = new InMemoryProductRepository();
        var product = CreateProduct("ab-100");
        repository.Add(product);

        var found = repository.GetBySku(" AB-100 ");

        Assert.Same(product, found);
    }

    [Fact]
    public void NewRepository_IsEmpty()
    {
        var repository = new InMemoryProductRepository();

        Assert.Equal(0, repository.Count);
        Assert.Empty(repository.GetAll());
        Assert.Null(repository.GetBySku("1001"));
    }
}
