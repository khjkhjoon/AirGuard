using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AirGuard.WPF.Services
{
    public class DatabaseService
    {
        private readonly string _dbPath;
        private string ConnectionString => $"Data Source={_dbPath}";

        public DatabaseService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "AirGuard");
            Directory.CreateDirectory(dir);
            _dbPath = Path.Combine(dir, "airguard.db");
            InitializeDatabase();
        }

        // ===== 초기화 =====
        private void InitializeDatabase()
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                PRAGMA journal_mode=WAL;

                CREATE TABLE IF NOT EXISTS users (
                    id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    username    TEXT NOT NULL UNIQUE,
                    password    TEXT NOT NULL,
                    role        TEXT NOT NULL DEFAULT 'Viewer',
                    name        TEXT NOT NULL,
                    created_at  TEXT NOT NULL,
                    last_login  TEXT
                );

                CREATE TABLE IF NOT EXISTS flight_logs (
                    id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    vehicle_id  TEXT NOT NULL,
                    name        TEXT NOT NULL,
                    latitude    REAL,
                    longitude   REAL,
                    altitude    REAL,
                    speed       REAL,
                    battery     REAL,
                    status      TEXT,
                    heading     REAL,
                    recorded_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS alerts (
                    id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    title       TEXT NOT NULL,
                    message     TEXT NOT NULL,
                    unit_id     TEXT,
                    severity    TEXT NOT NULL,
                    occurred_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS missions (
                    id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    vehicle_id  TEXT NOT NULL,
                    title       TEXT NOT NULL,
                    description TEXT,
                    status      TEXT NOT NULL DEFAULT 'Pending',
                    assigned_by TEXT,
                    created_at  TEXT NOT NULL,
                    completed_at TEXT
                );

                CREATE INDEX IF NOT EXISTS idx_flight_vehicle ON flight_logs(vehicle_id);
                CREATE INDEX IF NOT EXISTS idx_flight_time    ON flight_logs(recorded_at);
                CREATE INDEX IF NOT EXISTS idx_alerts_time    ON alerts(occurred_at);
            ";
            cmd.ExecuteNonQuery();

            // 기본 관리자 계정 생성
            EnsureDefaultAdmin(conn);
        }

        private void EnsureDefaultAdmin(SqliteConnection conn)
        {
            var check = conn.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM users WHERE username = 'admin'";
            long count = (long)(check.ExecuteScalar() ?? 0L);
            if (count > 0) return;

            var insert = conn.CreateCommand();
            insert.CommandText = @"
                INSERT INTO users (username, password, role, name, created_at)
                VALUES ('admin', @pw, 'Admin', '관리자', @now)";
            insert.Parameters.AddWithValue("@pw", HashPassword("admin1234"));
            insert.Parameters.AddWithValue("@now", DateTime.Now.ToString("o"));
            insert.ExecuteNonQuery();
        }

        // ===== 사용자 =====
        public UserRecord? Login(string username, string password)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, username, password, role, name FROM users WHERE username = @u";
            cmd.Parameters.AddWithValue("@u", username);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            string storedHash = reader.GetString(2);
            if (!VerifyPassword(password, storedHash)) return null;

            // 마지막 로그인 업데이트
            var update = conn.CreateCommand();
            update.CommandText = "UPDATE users SET last_login = @now WHERE username = @u";
            update.Parameters.AddWithValue("@now", DateTime.Now.ToString("o"));
            update.Parameters.AddWithValue("@u", username);
            update.ExecuteNonQuery();

            return new UserRecord
            {
                Id = reader.GetInt32(0),
                Username = reader.GetString(1),
                Role = reader.GetString(3),
                Name = reader.GetString(4)
            };
        }

        public List<UserRecord> GetAllUsers()
        {
            var list = new List<UserRecord>();
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, username, role, name, created_at, last_login FROM users ORDER BY id";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new UserRecord
                {
                    Id = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    Role = reader.GetString(2),
                    Name = reader.GetString(3),
                    CreatedAt = reader.GetString(4),
                    LastLogin = reader.IsDBNull(5) ? "" : reader.GetString(5)
                });
            }
            return list;
        }

        public bool CreateUser(string username, string password, string role, string name)
        {
            try
            {
                using var conn = new SqliteConnection(ConnectionString);
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO users (username, password, role, name, created_at)
                    VALUES (@u, @pw, @role, @name, @now)";
                cmd.Parameters.AddWithValue("@u", username);
                cmd.Parameters.AddWithValue("@pw", HashPassword(password));
                cmd.Parameters.AddWithValue("@role", role);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@now", DateTime.Now.ToString("o"));
                cmd.ExecuteNonQuery();
                return true;
            }
            catch { return false; }
        }

        public bool DeleteUser(int userId)
        {
            try
            {
                using var conn = new SqliteConnection(ConnectionString);
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM users WHERE id = @id AND username != 'admin'";
                cmd.Parameters.AddWithValue("@id", userId);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch { return false; }
        }

        // ===== 비행 로그 =====
        public void SaveFlightLog(string vehicleId, string name, double lat, double lon,
            double alt, double speed, double battery, string status, double heading)
        {
            try
            {
                using var conn = new SqliteConnection(ConnectionString);
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO flight_logs
                        (vehicle_id, name, latitude, longitude, altitude, speed, battery, status, heading, recorded_at)
                    VALUES
                        (@vid, @name, @lat, @lon, @alt, @spd, @bat, @stat, @hdg, @now)";
                cmd.Parameters.AddWithValue("@vid", vehicleId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@lat", lat);
                cmd.Parameters.AddWithValue("@lon", lon);
                cmd.Parameters.AddWithValue("@alt", alt);
                cmd.Parameters.AddWithValue("@spd", speed);
                cmd.Parameters.AddWithValue("@bat", battery);
                cmd.Parameters.AddWithValue("@stat", status);
                cmd.Parameters.AddWithValue("@hdg", heading);
                cmd.Parameters.AddWithValue("@now", DateTime.Now.ToString("o"));
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        public List<FlightLogRecord> GetFlightLogs(string vehicleId, int limit = 500)
        {
            var list = new List<FlightLogRecord>();
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT latitude, longitude, altitude, speed, battery, status, heading, recorded_at
                FROM flight_logs
                WHERE vehicle_id = @vid
                ORDER BY recorded_at DESC
                LIMIT @limit";
            cmd.Parameters.AddWithValue("@vid", vehicleId);
            cmd.Parameters.AddWithValue("@limit", limit);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new FlightLogRecord
                {
                    Latitude = reader.GetDouble(0),
                    Longitude = reader.GetDouble(1),
                    Altitude = reader.GetDouble(2),
                    Speed = reader.GetDouble(3),
                    Battery = reader.GetDouble(4),
                    Status = reader.GetString(5),
                    Heading = reader.GetDouble(6),
                    RecordedAt = DateTime.Parse(reader.GetString(7))
                });
            }
            return list;
        }

        // ===== 알림 =====
        public void SaveAlert(string title, string message, string unitId, string severity)
        {
            try
            {
                using var conn = new SqliteConnection(ConnectionString);
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO alerts (title, message, unit_id, severity, occurred_at)
                    VALUES (@title, @msg, @uid, @sev, @now)";
                cmd.Parameters.AddWithValue("@title", title);
                cmd.Parameters.AddWithValue("@msg", message);
                cmd.Parameters.AddWithValue("@uid", unitId);
                cmd.Parameters.AddWithValue("@sev", severity);
                cmd.Parameters.AddWithValue("@now", DateTime.Now.ToString("o"));
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        // ===== 비밀번호 =====
        private static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(password + "AirGuard_Salt_2024"));
            return Convert.ToBase64String(hash);
        }

        private static bool VerifyPassword(string password, string hash)
            => HashPassword(password) == hash;
    }

    // ===== 모델 =====
    public class UserRecord
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public string Role { get; set; } = "";
        public string Name { get; set; } = "";
        public string CreatedAt { get; set; } = "";
        public string LastLogin { get; set; } = "";

        public bool IsAdmin => Role == "Admin";
        public bool IsOperator => Role == "Admin" || Role == "Operator";
        public bool IsViewer => true;
    }

    public class FlightLogRecord
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public double Speed { get; set; }
        public double Battery { get; set; }
        public string Status { get; set; } = "";
        public double Heading { get; set; }
        public DateTime RecordedAt { get; set; }
    }
}