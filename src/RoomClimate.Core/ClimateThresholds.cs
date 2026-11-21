namespace RoomClimate.Core;

/// <summary>
/// Межі комфортного мікроклімату для навчальних/офісних приміщень
/// (орієнтовно за ДСН 3.3.6.042-99 та рекомендаціями ASHRAE щодо CO2).
/// </summary>
public sealed class ClimateThresholds
{
    public double TempMin { get; init; } = 20;
    public double TempMax { get; init; } = 24;
    public double TempCriticalLow { get; init; } = 16;
    public double TempCriticalHigh { get; init; } = 28;

    public double HumidityMin { get; init; } = 40;
    public double HumidityMax { get; init; } = 60;
    public double HumidityCriticalLow { get; init; } = 25;
    public double HumidityCriticalHigh { get; init; } = 75;

    public double Co2Warning { get; init; } = 1000;
    public double Co2Critical { get; init; } = 1500;

    public static ClimateThresholds Default { get; } = new();
}
