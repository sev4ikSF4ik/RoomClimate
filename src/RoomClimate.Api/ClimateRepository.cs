using Microsoft.Data.Sqlite;
using RoomClimate.Core;

namespace RoomClimate.Api;

/// <summary>Збереження вимірювань і сповіщень у SQLite (кросплатформна вбудована БД).</summary>
public sealed class ClimateRepository
{
    private readonly string _cs;

    public ClimateRepository(string connectionString)
    {
        _cs = connectionString;
        using var c = Open();
        Exec(c, """
            CREATE TABLE IF NOT EXISTS readings(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                room TEXT NOT NULL,
                ts INTEGER NOT NULL,
                temperature REAL NOT NULL,
                humidity REAL NOT NULL,
                co2 REAL NOT NULL);
            CREATE INDEX IF NOT EXISTS ix_readings_room_ts ON readings(room, ts);
            CREATE TABLE IF NOT EXISTS alerts(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                room TEXT NOT NULL,
                ts INTEGER NOT NULL,
                parameter TEXT NOT NULL,
                value REAL NOT NULL,
                level TEXT NOT NULL,
                message TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS ix_alerts_ts ON alerts(ts);
            """);
    }

    private SqliteConnection Open()
    {
        var c = new SqliteConnection(_cs);
        c.Open();
        return c;
    }

    private static void Exec(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Save(IReadOnlyCollection<Reading> readings, IReadOnlyCollection<Alert> alerts)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();

        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO readings(room, ts, temperature, humidity, co2) VALUES($r,$ts,$t,$h,$c)";
            var pr = cmd.Parameters.Add("$r", SqliteType.Text);
            var pts = cmd.Parameters.Add("$ts", SqliteType.Integer);
            var pt = cmd.Parameters.Add("$t", SqliteType.Real);
            var ph = cmd.Parameters.Add("$h", SqliteType.Real);
            var pc = cmd.Parameters.Add("$c", SqliteType.Real);
            foreach (var r in readings)
            {
                pr.Value = r.Room; pts.Value = r.Timestamp.ToUnixTimeMilliseconds();
                pt.Value = r.TemperatureC; ph.Value = r.HumidityPercent; pc.Value = r.Co2Ppm;
                cmd.ExecuteNonQuery();
            }
        }

        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO alerts(room, ts, parameter, value, level, message) VALUES($r,$ts,$p,$v,$l,$m)";
            var pr = cmd.Parameters.Add("$r", SqliteType.Text);
            var pts = cmd.Parameters.Add("$ts", SqliteType.Integer);
            var pp = cmd.Parameters.Add("$p", SqliteType.Text);
            var pv = cmd.Parameters.Add("$v", SqliteType.Real);
            var pl = cmd.Parameters.Add("$l", SqliteType.Text);
            var pm = cmd.Parameters.Add("$m", SqliteType.Text);
            foreach (var a in alerts)
            {
                pr.Value = a.Room; pts.Value = a.Timestamp.ToUnixTimeMilliseconds();
                pp.Value = a.Parameter; pv.Value = a.Value; pl.Value = a.Level.ToString(); pm.Value = a.Message;
                cmd.ExecuteNonQuery();
            }
        }

        tx.Commit();
    }

    public IReadOnlyList<string> Rooms()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT room FROM readings ORDER BY room";
        using var rd = cmd.ExecuteReader();
        var list = new List<string>();
        while (rd.Read()) list.Add(rd.GetString(0));
        return list;
    }

    public IReadOnlyList<Reading> Readings(string room, DateTimeOffset from, int limit = 2000)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT room, ts, temperature, humidity, co2 FROM readings
            WHERE room = $r AND ts >= $from ORDER BY ts DESC LIMIT $lim
            """;
        cmd.Parameters.AddWithValue("$r", room);
        cmd.Parameters.AddWithValue("$from", from.ToUnixTimeMilliseconds());
        cmd.Parameters.AddWithValue("$lim", limit);
        using var rd = cmd.ExecuteReader();
        var list = new List<Reading>();
        while (rd.Read())
            list.Add(new Reading(rd.GetString(0), DateTimeOffset.FromUnixTimeMilliseconds(rd.GetInt64(1)),
                rd.GetDouble(2), rd.GetDouble(3), rd.GetDouble(4)));
        list.Reverse();
        return list;
    }

    public IReadOnlyList<Reading> Latest()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT r.room, r.ts, r.temperature, r.humidity, r.co2 FROM readings r
            JOIN (SELECT room, MAX(ts) AS mts FROM readings GROUP BY room) m
              ON m.room = r.room AND m.mts = r.ts
            GROUP BY r.room ORDER BY r.room
            """;
        using var rd = cmd.ExecuteReader();
        var list = new List<Reading>();
        while (rd.Read())
            list.Add(new Reading(rd.GetString(0), DateTimeOffset.FromUnixTimeMilliseconds(rd.GetInt64(1)),
                rd.GetDouble(2), rd.GetDouble(3), rd.GetDouble(4)));
        return list;
    }

    public IReadOnlyList<Alert> Alerts(int limit = 50)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT room, ts, parameter, value, level, message FROM alerts ORDER BY ts DESC LIMIT $lim";
        cmd.Parameters.AddWithValue("$lim", limit);
        using var rd = cmd.ExecuteReader();
        var list = new List<Alert>();
        while (rd.Read())
            list.Add(new Alert(rd.GetString(0), DateTimeOffset.FromUnixTimeMilliseconds(rd.GetInt64(1)),
                rd.GetString(2), rd.GetDouble(3), Enum.Parse<AlertLevel>(rd.GetString(4)), rd.GetString(5)));
        return list;
    }
}
