using molodid.Models;
using molodid.Services;
using Microsoft.AspNetCore.Mvc;

namespace molodid.Controllers;

public class ProductsController : Controller
{
    private readonly IProductRepository _repository;
    private readonly IProductImportService _importService;
    private readonly IWebHostEnvironment _environment;

    public ProductsController(
        IProductRepository repository,
        IProductImportService importService,
        IWebHostEnvironment environment)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _importService = importService ?? throw new ArgumentNullException(nameof(importService));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    [HttpGet]
    public IActionResult Index(string searchString, string category, string sortOrder)
    {
        var products = _repository.GetAll().AsEnumerable();

        // Поиск по названию
        if (!string.IsNullOrWhiteSpace(searchString))
        {
            products = products.Where(p => p.Name.Contains(searchString, StringComparison.OrdinalIgnoreCase));
        }

        // Фильтрация по категории
        if (!string.IsNullOrWhiteSpace(category) && category != "Все категории")
        {
            products = products.Where(p => p.CategoryName == category);
        }

        // Сортировка
        products = sortOrder switch
        {
            "price_asc" => products.OrderBy(p => p.Price),
            "price_desc" => products.OrderByDescending(p => p.Price),
            "name" => products.OrderBy(p => p.Name),
            _ => products.OrderBy(p => p.Sku)
        };

        var viewModel = new ProductsIndexViewModel
        {
            Products = products.ToList(),
            SearchString = searchString ?? string.Empty,
            SelectedCategory = category ?? "Все категории",
            SortOrder = sortOrder ?? string.Empty,
            Categories = _repository.GetAll().Select(p => p.CategoryName).Distinct().ToList()
        };

        return View(viewModel);
    }

    [HttpGet]
    public IActionResult Import()
    {
        return View(new ImportViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Import(string? filePath)
    {
        var model = new ImportViewModel { FilePath = filePath?.Trim() ?? string.Empty };

        if (string.IsNullOrWhiteSpace(model.FilePath))
        {
            model.Result = ImportResult.Failed("Укажите путь к CSV-файлу");
            return View(model);
        }

        var resolved = ResolveFilePath(model.FilePath);
        model.Result = _importService.ImportFromCsv(resolved);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Clear()
    {
        _repository.Clear();
        return RedirectToAction(nameof(Index));
    }

    private string ResolveFilePath(string path)
    {
        if (path.StartsWith('/') && !path.StartsWith("//"))
        {
            var relative = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(_environment.WebRootPath, relative);
        }

        if (Path.IsPathRooted(path)) return path;

        return Path.Combine(_environment.WebRootPath, path.Replace('/', Path.DirectorySeparatorChar));
    }
}