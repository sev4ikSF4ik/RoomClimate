namespace RoomClimate.Core;

/// <summary>Одне вимірювання з сенсора приміщення.</summary>
public sealed record Reading(
    string Room,
    DateTimeOffset Timestamp,
    double TemperatureC,
    double HumidityPercent,
    double Co2Ppm);

public enum AlertLevel { Info, Warning, Critical }

/// <summary>Сповіщення про вихід параметра за межі норми.</summary>
public sealed record Alert(
    string Room,
    DateTimeOffset Timestamp,
    string Parameter,
    double Value,
    AlertLevel Level,
    string Message);

public enum ComfortLevel { Comfortable, Acceptable, Uncomfortable }

/// <summary>Підсумок стану приміщення за період.</summary>
public sealed record RoomSummary(
    string Room,
    int Count,
    double AvgTemperatureC,
    double AvgHumidityPercent,
    double AvgCo2Ppm,
    int ComfortIndex,
    ComfortLevel Comfort,
    string Trend,
    IReadOnlyList<string> Recommendations);
