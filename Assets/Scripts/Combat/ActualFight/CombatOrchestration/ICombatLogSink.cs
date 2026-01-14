public interface ICombatLogSink
{
    void LogLine(string line);
    void LogAdvanced(string prefix, int value, string suffix, CombatLogType type);
}
