namespace RoomClimate.Core;

/// <summary>
/// "Інтелектуальна" частина системи: валідація даних, генерація сповіщень,
/// індекс комфорту, тренд CO2 та рекомендації.
/// </summary>
public sealed class ClimateAnalyzer(ClimateThresholds? thresholds = null)
{
    private readonly ClimateThresholds _t = thresholds ?? ClimateThresholds.Default;

    /// <summary>Перевіряє, що значення фізично можливі (відсікає збої сенсора).</summary>
    public static bool IsValid(Reading r, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(r.Room)) error = "Room is required";
        else if (r.TemperatureC is < -40 or > 85 || double.IsNaN(r.TemperatureC)) error = "Temperature out of sensor range";
        else if (r.HumidityPercent is < 0 or > 100 || double.IsNaN(r.HumidityPercent)) error = "Humidity out of range";
        else if (r.Co2Ppm is < 300 or > 10000 || double.IsNaN(r.Co2Ppm)) error = "CO2 out of sensor range";
        return error is null;
    }

    public IReadOnlyList<Alert> Evaluate(Reading r)
    {
        var alerts = new List<Alert>();

        void Add(string p, double v, AlertLevel l, string m) =>
            alerts.Add(new Alert(r.Room, r.Timestamp, p, v, l, m));

        if (r.TemperatureC <= _t.TempCriticalLow) Add("temperature", r.TemperatureC, AlertLevel.Critical, "Критично холодно");
        else if (r.TemperatureC >= _t.TempCriticalHigh) Add("temperature", r.TemperatureC, AlertLevel.Critical, "Критично спекотно");
        else if (r.TemperatureC < _t.TempMin) Add("temperature", r.TemperatureC, AlertLevel.Warning, "Температура нижче норми");
        else if (r.TemperatureC > _t.TempMax) Add("temperature", r.TemperatureC, AlertLevel.Warning, "Температура вище норми");

        if (r.HumidityPercent <= _t.HumidityCriticalLow) Add("humidity", r.HumidityPercent, AlertLevel.Critical, "Дуже сухе повітря");
        else if (r.HumidityPercent >= _t.HumidityCriticalHigh) Add("humidity", r.HumidityPercent, AlertLevel.Critical, "Дуже висока вологість");
        else if (r.HumidityPercent < _t.HumidityMin) Add("humidity", r.HumidityPercent, AlertLevel.Warning, "Вологість нижче норми");
        else if (r.HumidityPercent > _t.HumidityMax) Add("humidity", r.HumidityPercent, AlertLevel.Warning, "Вологість вище норми");

        if (r.Co2Ppm >= _t.Co2Critical) Add("co2", r.Co2Ppm, AlertLevel.Critical, "Дуже високий CO2 — терміново провітріть");
        else if (r.Co2Ppm >= _t.Co2Warning) Add("co2", r.Co2Ppm, AlertLevel.Warning, "Підвищений CO2");

        return alerts;
    }

    /// <summary>Індекс комфорту 0..100: штрафи за відхилення кожного параметра від норми.</summary>
    public int ComfortIndex(double temp, double humidity, double co2)
    {
        double penalty = 0;
        if (temp < _t.TempMin) penalty += (_t.TempMin - temp) * 8;
        else if (temp > _t.TempMax) penalty += (temp - _t.TempMax) * 8;

        if (humidity < _t.HumidityMin) penalty += (_t.HumidityMin - humidity) * 1.5;
        else if (humidity > _t.HumidityMax) penalty += (humidity - _t.HumidityMax) * 1.5;

        if (co2 > 800) penalty += (co2 - 800) / 20;

        return (int)Math.Round(Math.Clamp(100 - penalty, 0, 100));
    }

    public static ComfortLevel ToLevel(int index) => index switch
    {
        >= 80 => ComfortLevel.Comfortable,
        >= 50 => ComfortLevel.Acceptable,
        _ => ComfortLevel.Uncomfortable
    };

    /// <summary>
    /// Тренд CO2 за нахилом лінійної регресії (ppm/хв).
    /// Повертає "rising", "falling" або "stable".
    /// </summary>
    public static string Co2Trend(IReadOnlyList<Reading> ordered)
    {
        if (ordered.Count < 3) return "stable";
        var t0 = ordered[0].Timestamp;
        double n = ordered.Count, sx = 0, sy = 0, sxy = 0, sxx = 0;
        foreach (var r in ordered)
        {
            double x = (r.Timestamp - t0).TotalMinutes, y = r.Co2Ppm;
            sx += x; sy += y; sxy += x * y; sxx += x * x;
        }
        double denom = n * sxx - sx * sx;
        if (Math.Abs(denom) < 1e-9) return "stable";
        double slope = (n * sxy - sx * sy) / denom;
        return slope > 5 ? "rising" : slope < -5 ? "falling" : "stable";
    }

    public RoomSummary Summarize(string room, IReadOnlyList<Reading> readings)
    {
        if (readings.Count == 0)
            return new RoomSummary(room, 0, 0, 0, 0, 0, ComfortLevel.Uncomfortable, "stable", ["Немає даних"]);

        var ordered = readings.OrderBy(r => r.Timestamp).ToList();
        double t = ordered.Average(r => r.TemperatureC);
        double h = ordered.Average(r => r.HumidityPercent);
        double c = ordered.Average(r => r.Co2Ppm);
        int idx = ComfortIndex(t, h, c);
        string trend = Co2Trend(ordered);

        var rec = new List<string>();
        if (c >= _t.Co2Warning || trend == "rising") rec.Add("Відкрийте вікно або увімкніть вентиляцію");
        if (t < _t.TempMin) rec.Add("Підвищте температуру опалення");
        if (t > _t.TempMax) rec.Add("Увімкніть кондиціонер або провітріть");
        if (h < _t.HumidityMin) rec.Add("Використайте зволожувач повітря");
        if (h > _t.HumidityMax) rec.Add("Використайте осушувач / провітріть");
        if (rec.Count == 0) rec.Add("Мікроклімат у нормі");

        return new RoomSummary(room, ordered.Count,
            Math.Round(t, 1), Math.Round(h, 1), Math.Round(c),
            idx, ToLevel(idx), trend, rec);
    }
}
