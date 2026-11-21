using RoomClimate.Core;

namespace RoomClimate.Tests;

public class ClimateAnalyzerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);
    private readonly ClimateAnalyzer _a = new();

    private static Reading R(double t, double h, double c, int minute = 0) =>
        new("Room 1", T0.AddMinutes(minute), t, h, c);

    [Fact]
    public void Normal_reading_produces_no_alerts()
    {
        Assert.Empty(_a.Evaluate(R(22, 50, 600)));
    }

    [Theory]
    [InlineData(18, "temperature", AlertLevel.Warning)]
    [InlineData(15, "temperature", AlertLevel.Critical)]
    [InlineData(26, "temperature", AlertLevel.Warning)]
    [InlineData(29, "temperature", AlertLevel.Critical)]
    public void Temperature_out_of_range_raises_alert(double temp, string param, AlertLevel level)
    {
        var alert = Assert.Single(_a.Evaluate(R(temp, 50, 600)));
        Assert.Equal(param, alert.Parameter);
        Assert.Equal(level, alert.Level);
    }

    [Theory]
    [InlineData(1100, AlertLevel.Warning)]
    [InlineData(1600, AlertLevel.Critical)]
    public void High_co2_raises_alert(double co2, AlertLevel level)
    {
        var alert = Assert.Single(_a.Evaluate(R(22, 50, co2)));
        Assert.Equal("co2", alert.Parameter);
        Assert.Equal(level, alert.Level);
    }

    [Fact]
    public void Multiple_problems_raise_multiple_alerts()
    {
        Assert.Equal(3, _a.Evaluate(R(30, 80, 2000)).Count);
    }

    [Fact]
    public void Comfort_index_is_100_for_ideal_conditions()
    {
        Assert.Equal(100, _a.ComfortIndex(22, 50, 600));
        Assert.Equal(ComfortLevel.Comfortable, ClimateAnalyzer.ToLevel(100));
    }

    [Fact]
    public void Comfort_index_drops_with_bad_conditions_and_is_clamped()
    {
        Assert.True(_a.ComfortIndex(22, 50, 1400) < 80);
        Assert.Equal(0, _a.ComfortIndex(35, 90, 3000));
        Assert.Equal(ComfortLevel.Uncomfortable, ClimateAnalyzer.ToLevel(0));
    }

    [Theory]
    [InlineData("", 22, 50, 600)]
    [InlineData("A", 120, 50, 600)]
    [InlineData("A", 22, 140, 600)]
    [InlineData("A", 22, 50, 50)]
    public void Invalid_readings_are_rejected(string room, double t, double h, double c)
    {
        Assert.False(ClimateAnalyzer.IsValid(new Reading(room, T0, t, h, c), out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Valid_reading_is_accepted()
    {
        Assert.True(ClimateAnalyzer.IsValid(R(22, 50, 600), out _));
    }

    [Fact]
    public void Co2_trend_detects_rising_falling_and_stable()
    {
        var rising = Enumerable.Range(0, 10).Select(i => R(22, 50, 500 + i * 30, i)).ToList();
        var falling = Enumerable.Range(0, 10).Select(i => R(22, 50, 1500 - i * 30, i)).ToList();
        var stable = Enumerable.Range(0, 10).Select(i => R(22, 50, 600 + (i % 2), i)).ToList();

        Assert.Equal("rising", ClimateAnalyzer.Co2Trend(rising));
        Assert.Equal("falling", ClimateAnalyzer.Co2Trend(falling));
        Assert.Equal("stable", ClimateAnalyzer.Co2Trend(stable));
    }

    [Fact]
    public void Summary_recommends_ventilation_when_co2_is_high()
    {
        var data = Enumerable.Range(0, 5).Select(i => R(22, 50, 1200 + i * 50, i)).ToList();
        var s = _a.Summarize("Room 1", data);

        Assert.Equal(5, s.Count);
        Assert.Contains(s.Recommendations, r => r.Contains("вікно"));
        Assert.Equal("rising", s.Trend);
    }

    [Fact]
    public void Summary_of_empty_data_is_safe()
    {
        var s = _a.Summarize("Empty", []);
        Assert.Equal(0, s.Count);
    }
}
