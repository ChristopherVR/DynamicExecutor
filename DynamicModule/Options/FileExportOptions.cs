namespace DynamicModule.Options;

public sealed record FileExportOptions
{
    public string ExportPath { get; set; } = string.Empty;
}
