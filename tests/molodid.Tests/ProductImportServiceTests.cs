using molodid.Models;
using molodid.Services;

namespace molodid.Tests;

public class ProductImportServiceTests
{
    private const string Header = "sku,name,description,price,currency,category_name,stock,image_url,is_active,brand,warranty_months,model_number";
    private const string Row1001 = "1001,Ноутбук Lenovo IdeaPad 3,Бюджетный ноутбук для работы и учебы,45990,RUB,Компьютеры,5,/images/laptop.jpg,true,Lenovo,24,IdeaPad3-15";
    private const string Row1002 = "1002,Мышь Logitech M185,Беспроводная мышь с USB-приемником,1290,RUB,Периферия,10,/images/mouse.jpg,true,Logitech,12,M185";
    private const string Row1011 = "1011,Блок питания Corsair CV550,,4990,RUB,Компьютеры,9,,true,Corsair,60,CV550";

    private static string Row(
        string sku = "1001",
        string name = "Ноутбук Lenovo IdeaPad 3",
        string description = "Бюджетный ноутбук для работы и учебы",
        string price = "45990",
        string currency = "RUB",
        string category = "Компьютеры",
        string stock = "5",
        string imageUrl = "/images/laptop.jpg",
        string isActive = "true",
        string brand = "Lenovo",
        string warranty = "24",
        string modelNumber = "IdeaPad3-15")
        => string.Join(',', sku, name, description, price, currency, category, stock, imageUrl, isActive, brand, warranty, modelNumber);

    private static (InMemoryProductRepository Repository, ProductImportService Service) CreateService()
    {
        var repository = new InMemoryProductRepository();
        return (repository, new ProductImportService(repository));
    }

    private static string TestDataPath(string fileName)
        => Path.Combine(AppContext.BaseDirectory, "testdata", fileName);

    private static void AssertSingleRowRejected(string row, string expectedMessage)
    {
        var (repository, service) = CreateService();

        var result = service.ImportFromLines(new[] { Header, row });

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.ImportedCount);
        Assert.Contains(result.Errors, e => e.Message.Contains(expectedMessage));
        Assert.Equal(0, repository.Count);
    }

    [Fact]
    public void ImportFromLines_HeaderAndOneValidRow_ImportsProduct()
    {
        var (repository, service) = CreateService();

        var result = service.ImportFromLines(new[] { Header, Row1001 });

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(1, result.ImportedCount);
        Assert.Empty(result.Errors);
        var product = repository.GetBySku("1001");
        Assert.NotNull(product);
        Assert.Equal("Ноутбук Lenovo IdeaPad 3", product.Name);
        Assert.Equal(45990m, product.Price);
        Assert.Equal("Lenovo", product.Brand);
        Assert.Equal(24, product.WarrantyMonths);
        Assert.Equal("IdeaPad3-15", product.ModelNumber);
    }

    [Fact]
    public void ImportFromLines_HeaderAndTwoValidRows_ImportsBoth()
    {
        var (repository, service) = CreateService();

        var result = service.ImportFromLines(new[] { Header, Row1001, Row1002 });

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.ImportedCount);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(2, repository.Count);
    }

    [Fact]
    public void ImportFromCsv_FileDoesNotExist_ReturnsFileNotFoundError()
    {
        var (_, service) = CreateService();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");

        var result = service.ImportFromCsv(path);

        Assert.Equal(0, result.TotalRows);
        Assert.Equal(0, result.ImportedCount);
        var error = Assert.Single(result.Errors);
        Assert.Contains("Файл не найден", error.Message);
    }

    [Fact]
    public void ImportFromCsv_EmptyTempFile_ReturnsNoDataError()
    {
        var (_, service) = CreateService();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        File.WriteAllText(path, string.Empty);

        try
        {
            var result = service.ImportFromCsv(path);

            Assert.Equal(0, result.TotalRows);
            Assert.Equal(0, result.ImportedCount);
            var error = Assert.Single(result.Errors);
            Assert.Contains("Данные для импорта отсутствуют", error.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ImportFromLines_OnlyHeader_ReturnsNoDataError()
    {
        var (_, service) = CreateService();

        var result = service.ImportFromLines(new[] { Header });

        Assert.Equal(0, result.TotalRows);
        Assert.Equal(0, result.ImportedCount);
        var error = Assert.Single(result.Errors);
        Assert.Contains("Данные для импорта отсутствуют", error.Message);
    }

    [Fact]
    public void ImportFromLines_HeaderWithoutElectronicsColumns_ReturnsInvalidHeaderError()
    {
        var (repository, service) = CreateService();
        var shortHeader = "sku,name,description,price,currency,category_name,stock,image_url,is_active";

        var result = service.ImportFromLines(new[] { shortHeader, Row1001 });

        Assert.Equal(0, result.TotalRows);
        Assert.Equal(0, result.ImportedCount);
        Assert.Contains(result.Errors, e => e.Message.Contains("Неверные заголовки CSV"));
        Assert.Equal(0, repository.Count);
    }

    [Fact]
    public void ImportFromLines_RowWithWrongFieldCount_ReturnsFieldCountError()
    {
        AssertSingleRowRejected("1001,Ноутбук Lenovo IdeaPad 3", "Неверное количество полей в строке");
    }

    [Fact]
    public void ImportFromLines_EmptySku_ReturnsSkuError()
    {
        AssertSingleRowRejected(Row(sku: ""), "Поле sku не может быть пустым");
    }

    [Fact]
    public void ImportFromLines_EmptyName_ReturnsNameError()
    {
        AssertSingleRowRejected(Row(name: ""), "Поле name не может быть пустым");
    }

    [Fact]
    public void ImportFromLines_NegativePrice_ReturnsPriceError()
    {
        AssertSingleRowRejected(Row(price: "-500"), "Поле price должно быть числом больше 0");
    }

    [Fact]
    public void ImportFromLines_NonNumericPrice_ReturnsPriceError()
    {
        AssertSingleRowRejected(Row(price: "abc"), "Поле price должно быть числом больше 0");
    }

    [Fact]
    public void ImportFromLines_NegativeStock_ReturnsStockError()
    {
        AssertSingleRowRejected(Row(stock: "-5"), "Остаток на складе должен быть целым числом не меньше 0");
    }

    [Fact]
    public void ImportFromLines_InvalidIsActive_ReturnsIsActiveError()
    {
        AssertSingleRowRejected(Row(isActive: "yes"), "Поле is_active должно иметь значение true, false, 1 или 0");
    }

    [Fact]
    public void ImportFromLines_EmptyBrand_ReturnsBrandError()
    {
        AssertSingleRowRejected(Row(brand: ""), "Поле brand обязательно для варианта «Электроника»");
    }

    [Fact]
    public void ImportFromLines_NegativeWarranty_ReturnsWarrantyError()
    {
        AssertSingleRowRejected(Row(warranty: "-1"), "Гарантия должна быть целым числом от 0 до 60 месяцев");
    }

    [Fact]
    public void ImportFromLines_WarrantyAboveMaximum_ReturnsWarrantyError()
    {
        AssertSingleRowRejected(Row(warranty: "61"), "Гарантия должна быть целым числом от 0 до 60 месяцев");
    }

    [Fact]
    public void ImportFromLines_DuplicateSkuInsideFile_KeepsFirstProduct()
    {
        var (repository, service) = CreateService();
        var duplicate = Row(name: "Ноутбук ASUS VivoBook", price: "52990", brand: "ASUS", modelNumber: "VivoBook");

        var result = service.ImportFromLines(new[] { Header, Row1001, duplicate });

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.ImportedCount);
        var error = Assert.Single(result.Errors);
        Assert.Contains("Артикул товара уже существует", error.Message);
        Assert.Equal(1, repository.Count);
        Assert.Equal("Ноутбук Lenovo IdeaPad 3", repository.GetBySku("1001")!.Name);
    }

    [Fact]
    public void ImportFromLines_SkuAlreadyInRepository_DoesNotAddProduct()
    {
        var (repository, service) = CreateService();
        repository.Add(new Product { Sku = "1001", Name = "Существующий товар" });

        var result = service.ImportFromLines(new[] { Header, Row1001 });

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.ImportedCount);
        Assert.Contains(result.Errors, e => e.Message.Contains("Артикул товара уже существует"));
        Assert.Equal(1, repository.Count);
        Assert.Equal("Существующий товар", repository.GetBySku("1001")!.Name);
    }

    [Fact]
    public void ImportFromLines_SeveralErrorsInOneRow_ReportsAllErrors()
    {
        var (repository, service) = CreateService();
        var row = ",,Некорректная строка,-10,RUB,Компьютеры,-5,/images/x.jpg,yes,,61,";

        var result = service.ImportFromLines(new[] { Header, row });

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.ImportedCount);
        Assert.True(result.ErrorCount >= 4);
        Assert.Equal(0, repository.Count);
    }

    [Fact]
    public void ImportFromLines_EmptyDescriptionAndImageUrl_ImportsProduct()
    {
        var (repository, service) = CreateService();

        var result = service.ImportFromLines(new[] { Header, Row1011 });

        Assert.Equal(1, result.ImportedCount);
        Assert.Empty(result.Errors);
        var product = repository.GetBySku("1011");
        Assert.NotNull(product);
        Assert.Equal(string.Empty, product.Description);
        Assert.Equal(string.Empty, product.ImageUrl);
    }

    [Fact]
    public void ImportFromLines_EmptyCurrency_UsesRub()
    {
        var (repository, service) = CreateService();

        var result = service.ImportFromLines(new[] { Header, Row(currency: "") });

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal("RUB", repository.GetBySku("1001")!.Currency);
    }

    [Fact]
    public void ImportFromLines_EmptyCategory_UsesDefaultCategory()
    {
        var (repository, service) = CreateService();

        var result = service.ImportFromLines(new[] { Header, Row(category: "") });

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal("Без категории", repository.GetBySku("1001")!.CategoryName);
    }

    [Fact]
    public void ImportFromLines_CyrillicText_IsPreserved()
    {
        var (repository, service) = CreateService();

        service.ImportFromLines(new[] { Header, Row1001 });

        var product = repository.GetBySku("1001");
        Assert.NotNull(product);
        Assert.Equal("Ноутбук Lenovo IdeaPad 3", product.Name);
        Assert.Equal("Бюджетный ноутбук для работы и учебы", product.Description);
        Assert.Equal("Компьютеры", product.CategoryName);
    }

    [Fact]
    public void ImportFromCsv_ValidElectronicsFile_ImportsAllRows()
    {
        var (repository, service) = CreateService();

        var result = service.ImportFromCsv(TestDataPath("valid_electronics.csv"));

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.ImportedCount);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(2, repository.Count);
    }

    [Fact]
    public void ImportFromCsv_InvalidPriceFile_ReportsPriceErrorForRowTwo()
    {
        var (repository, service) = CreateService();

        var result = service.ImportFromCsv(TestDataPath("invalid_price.csv"));

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.ImportedCount);
        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.RowNumber);
        Assert.Contains("Поле price должно быть числом больше 0", error.Message);
        Assert.Equal(0, repository.Count);
    }

    [Fact]
    public void ImportFromCsv_DuplicateSkuFile_KeepsOnlyFirstProduct()
    {
        var (repository, service) = CreateService();

        var result = service.ImportFromCsv(TestDataPath("duplicate_sku.csv"));

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.ImportedCount);
        var error = Assert.Single(result.Errors);
        Assert.Contains("Артикул товара уже существует", error.Message);
        Assert.Equal(1, repository.Count);
        Assert.Equal("Ноутбук Lenovo IdeaPad 3", repository.GetBySku("1001")!.Name);
    }

    [Fact]
    public void ImportFromCsv_MissingBrandFile_ReportsBrandError()
    {
        var (repository, service) = CreateService();

        var result = service.ImportFromCsv(TestDataPath("missing_brand.csv"));

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.ImportedCount);
        var error = Assert.Single(result.Errors);
        Assert.Contains("Поле brand обязательно", error.Message);
        Assert.Equal(0, repository.Count);
    }

    [Fact]
    public void ImportFromCsv_WarrantyOutOfRangeFile_ReportsWarrantyError()
    {
        var (repository, service) = CreateService();

        var result = service.ImportFromCsv(TestDataPath("warranty_out_of_range.csv"));

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.ImportedCount);
        var error = Assert.Single(result.Errors);
        Assert.Contains("Гарантия должна быть целым числом от 0 до 60 месяцев", error.Message);
        Assert.Equal(0, repository.Count);
    }

    [Fact]
    public void ImportFromCsv_BadActiveFile_ReportsIsActiveError()
    {
        var (repository, service) = CreateService();

        var result = service.ImportFromCsv(TestDataPath("bad_active.csv"));

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.ImportedCount);
        var error = Assert.Single(result.Errors);
        Assert.Contains("Поле is_active должно иметь значение", error.Message);
        Assert.Equal(0, repository.Count);
    }

    [Fact]
    public void ImportFromCsv_EmptyFile_ReturnsNoDataError()
    {
        var (repository, service) = CreateService();

        var result = service.ImportFromCsv(TestDataPath("empty_file.csv"));

        Assert.Equal(0, result.TotalRows);
        Assert.Equal(0, result.ImportedCount);
        var error = Assert.Single(result.Errors);
        Assert.Contains("Данные для импорта отсутствуют", error.Message);
        Assert.Equal(0, repository.Count);
    }
}
