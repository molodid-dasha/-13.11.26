using molodid.Models;

namespace molodid.Services;

public interface IProductImportService
{
    ImportResult ImportFromCsv(string filePath);
    ImportResult ImportFromLines(IEnumerable<string> lines);
}