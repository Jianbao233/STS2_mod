namespace RunHistoryAnalyzer.Models;

public interface IAnomalyRule
{
    string Name { get; }
    string DisplayName { get; }

    Anomaly? Check(RunHistoryData history);
}
