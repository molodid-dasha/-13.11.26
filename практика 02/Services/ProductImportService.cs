using System.Globalization;
using molodid.Models;

namespace molodid.Services;

/// <summary>
/// Сервис импорта товаров из CSV (вариант «Электроника»).
/// </summary>
public class ProductImportService : IProductImportService
{
    private const string DefaultCurrency = "RUB";
    private const string DefaultCategory = "Без категории";
    private const int MinWarranty = 0;
    private const int MaxWarranty = 60;

    private static readonly string[] RequiredColumns =
    {
        "sku", "name", "description", "price", "currency",
        "category_name", "stock", "image_url", "is_active",
        "brand", "warranty_months", "model_number"
    };

    private readonly IProductRepository _repository;

    public ProductImportService(IProductRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    // Импорт из CSV-файла. Проверяет существование файла, читает строки и передаёт в ImportFromLines.
    public ImportResult ImportFromCsv(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return ImportResult.Failed("Файл не найден");

        try
        {
            var lines = File.ReadAllLines(filePath);
            return ImportFromLines(lines);
        }
        catch (IOException ex)
        {
            return ImportResult.Failed($"Ошибка чтения файла: {ex.Message}");
        }
        catch (UnauthorizedAccessException)
        {
            return ImportResult.Failed("Нет доступа к файлу");
        }
    }

    // Импорт из набора строк. Читает заголовок, проверяет наличие всех колонок, валидирует каждую строку данных.
    public ImportResult ImportFromLines(IEnumerable<string> lines)
    {
        var result = new ImportResult();
        var allLines = lines.ToList();

        int headerIndex = allLines.FindIndex(l => !string.IsNullOrWhiteSpace(l));
        if (headerIndex < 0)
            return ImportResult.Failed("Данные для импорта отсутствуют");

        var header = CsvLineParser.Parse(allLines[headerIndex]);
        var columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < header.Count; i++)
        {
            var name = header[i].Trim().TrimStart('\uFEFF');
            if (name.Length > 0 && !columnMap.ContainsKey(name))
                columnMap[name] = i;
        }

        var missing = RequiredColumns.Where(c => !columnMap.ContainsKey(c)).ToList();
        if (missing.Count > 0)
        {
            result.Errors.Add(new ImportError
            {
                RowNumber = headerIndex + 1,
                Message = "Неверные заголовки CSV: нет колонок " + string.Join(", ", missing)
            });
            return result;
        }

        var importedSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = headerIndex + 1; i < allLines.Count; i++)
        {
            var raw = allLines[i];
            if (string.IsNullOrWhiteSpace(raw)) continue;

            int rowNumber = i + 1;
            result.TotalRows++;

            var values = CsvLineParser.Parse(raw);
            if (values.Count != header.Count)
            {
                result.Errors.Add(new ImportError
                {
                    RowNumber = rowNumber,
                    Message = $"Неверное количество полей в строке: ожидалось {header.Count}, получено {values.Count}"
                });
                continue;
            }

            string Get(string column) =>
                columnMap.TryGetValue(column, out var idx) && idx < values.Count
                    ? values[idx] : string.Empty;

            var rowErrors = new List<ImportError>();

            string sku = Get("sku").Trim();
            string name = Get("name").Trim();
            string brand = Get("brand").Trim();

            if (string.IsNullOrWhiteSpace(sku))
                rowErrors.Add(new ImportError { RowNumber = rowNumber, Message = "Поле sku не может быть пустым" });

            if (string.IsNullOrWhiteSpace(name))
                rowErrors.Add(new ImportError { RowNumber = rowNumber, Message = "Поле name не может быть пустым" });

            if (!decimal.TryParse(Get("price"), NumberStyles.Number, CultureInfo.InvariantCulture, out var price) || price <= 0)
                rowErrors.Add(new ImportError { RowNumber = rowNumber, Message = "Поле price должно быть числом больше 0" });

            if (!int.TryParse(Get("stock"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var stock) || stock < 0)
                rowErrors.Add(new ImportError { RowNumber = rowNumber, Message = "Остаток на складе должен быть целым числом не меньше 0" });

            if (!TryParseBool(Get("is_active"), out var isActive))
                rowErrors.Add(new ImportError { RowNumber = rowNumber, Message = "Поле is_active должно иметь значение true, false, 1 или 0" });

            if (string.IsNullOrWhiteSpace(brand))
                rowErrors.Add(new ImportError { RowNumber = rowNumber, Message = "Поле brand обязательно для варианта «Электроника»" });

            if (!int.TryParse(Get("warranty_months"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var warranty)
                || warranty < MinWarranty || warranty > MaxWarranty)
                rowErrors.Add(new ImportError { RowNumber = rowNumber, Message = "Гарантия должна быть целым числом от 0 до 60 месяцев" });

            if (rowErrors.Count > 0)
            {
                result.Errors.AddRange(rowErrors);
                continue;
            }

            if (_repository.GetBySku(sku) != null || importedSkus.Contains(sku))
            {
                result.Errors.Add(new ImportError
                {
                    RowNumber = rowNumber,
                    Message = "Артикул товара уже существует"
                });
                continue;
            }

            var product = new Product
            {
                Sku = sku,
                Name = name,
                Description = Get("description").Trim(),
                Price = price,
                Currency = NormalizeCurrency(Get("currency")),
                CategoryName = NormalizeCategory(Get("category_name")),
                StockQuantity = stock,
                ImageUrl = Get("image_url").Trim(),
                IsActive = isActive,
                Brand = brand,
                WarrantyMonths = warranty,
                ModelNumber = Get("model_number").Trim()
            };

            if (_repository.Add(product))
            {
                importedSkus.Add(sku);
                result.ImportedCount++;
            }
        }

        if (result.TotalRows == 0 && result.Errors.Count == 0)
            result.Errors.Add(new ImportError { RowNumber = 0, Message = "Данные для импорта отсутствуют" });

        return result;
    }

    // Преобразует true/false/1/0 в bool. Возвращает false, если значение не распознано.
    private static bool TryParseBool(string value, out bool result)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "true":
            case "1":
                result = true;
                return true;
            case "false":
            case "0":
                result = false;
                return true;
            default:
                result = false;
                return false;
        }
    }

    private static string NormalizeCurrency(string value)
        => string.IsNullOrWhiteSpace(value) ? DefaultCurrency : value.Trim().ToUpperInvariant();

    private static string NormalizeCategory(string value)
        => string.IsNullOrWhiteSpace(value) ? DefaultCategory : value.Trim();
}