namespace RunHistoryAnalyzer.Models;

public sealed class Anomaly
{
    public AnomalyLevel Level { get; }
    public string RuleName { get; }
    public string Title { get; }
    public string Description { get; }
    public string Detail { get; }
    public string PossibleCause { get; }

    public Anomaly(AnomalyLevel level, string ruleName, string title, string description, string detail, string possibleCause)
    {
        Level = level;
        RuleName = ruleName;
        Title = title;
        Description = description;
        Detail = detail;
        PossibleCause = possibleCause;
    }
}
