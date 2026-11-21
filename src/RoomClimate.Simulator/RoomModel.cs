using RoomClimate.Core;

namespace RoomClimate.Simulator;

/// <summary>
/// Проста фізична модель приміщення: люди підвищують CO2, температуру та вологість,
/// періодичне провітрювання знижує їх. Додається шум сенсора.
/// </summary>
public sealed class RoomModel(string name, int seed)
{
    private readonly Random _rnd = new(seed);
    private double _temp = 21 + new Random(seed).NextDouble() * 2;
    private double _hum = 45;
    private double _co2 = 450;
    private int _people = 10 + seed % 15;
    private int _ventilateTicks;

    public string Name { get; } = name;

    public Reading Next(DateTimeOffset now)
    {
        // Люди заходять/виходять
        if (_rnd.NextDouble() < 0.15) _people = Math.Clamp(_people + _rnd.Next(-5, 8), 0, 35);

        // Інколи хтось відкриває вікно
        if (_ventilateTicks == 0 && (_co2 > 1300 || _rnd.NextDouble() < 0.02)) _ventilateTicks = _rnd.Next(5, 15);

        bool vent = _ventilateTicks > 0;
        if (vent) _ventilateTicks--;

        _co2 += _people * 4 - (vent ? 120 + (_co2 - 420) * 0.15 : 3);
        _temp += _people * 0.01 - (vent ? 0.15 : 0.005) + (21.5 - _temp) * 0.01;
        _hum += _people * 0.05 - (vent ? 0.6 : 0.02) + (45 - _hum) * 0.01;

        _co2 = Math.Clamp(_co2, 420, 3000);
        _temp = Math.Clamp(_temp, 14, 32);
        _hum = Math.Clamp(_hum, 20, 85);

        double Noise(double amp) => (_rnd.NextDouble() - 0.5) * 2 * amp;

        return new Reading(Name, now,
            Math.Round(_temp + Noise(0.1), 2),
            Math.Round(_hum + Noise(0.5), 1),
            Math.Round(_co2 + Noise(10)));
    }
}
