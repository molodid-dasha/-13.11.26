namespace molodid.Models;

public class ImportResult
{
    public int TotalRows { get; set; }
    public int ImportedCount { get; set; }
    public List<ImportError> Errors { get; set; } = new();
    public int ErrorCount => Errors.Count;

    public static ImportResult Failed(string message)
    {
        var result = new ImportResult();
        result.Errors.Add(new ImportError { RowNumber = 0, Message = message });
        return result;
    }
}