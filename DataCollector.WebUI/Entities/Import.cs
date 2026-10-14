namespace DataCollector.WebUI.Entities;

public class ImportOptions
{
  public bool UpdateExisting { get; set; } = true;
  public bool SkipInvalid { get; set; }
  public bool KeepCreatedAt { get; set; } = true;
}

public class ImportResult
{
  public string Format { get; set; } = null!;
  public bool DryRun { get; set; }
  public bool Applied { get; set; }
  public int TotalRows { get; set; }
  public int Created { get; set; }
  public int Updated { get; set; }
  public int Invalid { get; set; }
  public List<ImportError> Errors { get; set; } = [];
  public int ErrorCount { get; set; }
  public List<ImportColumn> Columns { get; set; } = [];

  public int Valid => Created + Updated;
}

public class ImportError
{
  public int Row { get; set; }
  public string? Column { get; set; }
  public string Message { get; set; } = null!;
}

public class ImportColumn
{
  public string Header { get; set; } = null!;

  /// <summary>field, id, createdAt або ignored.</summary>
  public string Target { get; set; } = null!;

  public string? FieldKey { get; set; }
  public string? FieldName { get; set; }
  public string? Note { get; set; }

  public bool IsUsed => Target != "ignored";
}
