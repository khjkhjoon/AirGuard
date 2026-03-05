using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace AirGuard.WPF.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(string host = "localhost", int port = 3306,
            string database = "airguard", string user = "root", string password = "")
        {
            _connectionString =
                $"Server={host};Port={port};Database={database};" +
                $"User={user};Password={password};" +
                $"CharSet=utf8mb4;AllowPublicKeyRetrieval=true;SslMode=None;";
            InitializeDatabase();
        }

        // ===== 초기화 =====
        private void InitializeDatabase()
        {
            // DB가 없으면 먼저 생성
            string rootConn = _connectionString.Replace(
                $"Database={GetDatabaseName()};", "");
            using (var conn = new MySqlConnection(rootConn))
            {
                conn.Open();
                var createDb = conn.CreateCommand();
                createDb.CommandText =
                    $"CREATE DATABASE IF NOT EXISTS `{GetDatabaseName()}` " +
                    $"CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
                createDb.ExecuteNonQuery();
            }

            using var c = new MySqlConnection(_connectionString);
            c.Open();
            var cmd = c.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS users (
                    id          INT AUTO_INCREMENT PRIMARY KEY,
                    username    VARCHAR(50)  NOT NULL UNIQUE,
                    password    VARCHAR(256) NOT NULL,
                    role        VARCHAR(20)  NOT NULL DEFAULT 'Viewer',
                    name        VARCHAR(50)  NOT NULL,
                    created_at  DATETIME     NOT NULL,
                    last_login  DATETIME
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

                CREATE TABLE IF NOT EXISTS flight_logs (
                    id          INT AUTO_INCREMENT PRIMARY KEY,
                    vehicle_id  VARCHAR(50)  NOT NULL,
                    name        VARCHAR(100) NOT NULL,
                    latitude    DOUBLE,
                    longitude   DOUBLE,
                    altitude    DOUBLE,
                    speed       DOUBLE,
                    battery     DOUBLE,
                    status      VARCHAR(20),
                    heading     DOUBLE,
                    recorded_at DATETIME NOT NULL,
                    INDEX idx_vehicle (vehicle_id),
                    INDEX idx_time    (recorded_at)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

                CREATE TABLE IF NOT EXISTS alerts (
                    id          INT AUTO_INCREMENT PRIMARY KEY,
                    title       VARCHAR(100) NOT NULL,
                    message     VARCHAR(500) NOT NULL,
                    unit_id     VARCHAR(50),
                    severity    VARCHAR(20)  NOT NULL,
                    occurred_at DATETIME     NOT NULL,
                    INDEX idx_time (occurred_at)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

                CREATE TABLE IF NOT EXISTS missions (
                    id           INT AUTO_INCREMENT PRIMARY KEY,
                    vehicle_id   VARCHAR(50)  NOT NULL,
                    title        VARCHAR(100) NOT NULL,
                    description  TEXT,
                    status       VARCHAR(20)  NOT NULL DEFAULT 'Pending',
                    assigned_by  VARCHAR(50),
                    created_at   DATETIME NOT NULL,
                    completed_at DATETIME,
                    INDEX idx_vehicle (vehicle_id)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
            ";
            cmd.ExecuteNonQuery();

            EnsureDefaultAdmin(c);
        }

        private void EnsureDefaultAdmin(MySqlConnection conn)
        {
            var check = conn.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM users WHERE username = 'admin'";
            long count = Convert.ToInt64(check.ExecuteScalar());
            if (count > 0) return;

            var insert = conn.CreateCommand();
            insert.CommandText = @"
                INSERT INTO users (username, password, role, name, created_at)
                VALUES (@u, @pw, 'Admin', '관리자', @now)";
            insert.Parameters.AddWithValue("@u", "admin");
            insert.Parameters.AddWithValue("@pw", HashPassword("admin1234"));
            insert.Parameters.AddWithValue("@now", DateTime.Now);
            insert.ExecuteNonQuery();
        }

        private string GetDatabaseName()
        {
            foreach (var part in _connectionString.Split(';'))
            {
                var kv = part.Trim().Split('=');
                if (kv.Length == 2 && kv[0].Trim().ToLower() == "database")
                    return kv[1].Trim();
            }
            return "airguard";
        }

        // ===== 로그인 =====
        public UserRecord? Login(string username, string password)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT id, username, password, role, name FROM users WHERE username = @u";
            cmd.Parameters.AddWithValue("@u", username);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            if (!VerifyPassword(password, reader.GetString(2))) return null;

            var user = new UserRecord
            {
                Id = reader.GetInt32(0),
                Username = reader.GetString(1),
                Role = reader.GetString(3),
                Name = reader.GetString(4)
            };
            reader.Close();

            var update = conn.CreateCommand();
            update.CommandText =
                "UPDATE users SET last_login = @now WHERE username = @u";
            update.Parameters.AddWithValue("@now", DateTime.Now);
            update.Parameters.AddWithValue("@u", username);
            update.ExecuteNonQuery();

            return user;
        }

        // ===== 사용자 관리 =====
        public List<UserRecord> GetAllUsers()
        {
            var list = new List<UserRecord>();
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT id, username, role, name, created_at, last_login " +
                "FROM users ORDER BY id";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new UserRecord
                {
                    Id = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    Role = reader.GetString(2),
                    Name = reader.GetString(3),
                    CreatedAt = reader.GetDateTime(4).ToString("yyyy-MM-dd HH:mm"),
                    LastLogin = reader.IsDBNull(5) ? "" :
                                reader.GetDateTime(5).ToString("yyyy-MM-dd HH:mm")
                });
            }
            return list;
        }

        public bool CreateUser(string username, string password, string role, string name)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO users (username, password, role, name, created_at)
                    VALUES (@u, @pw, @role, @name, @now)";
                cmd.Parameters.AddWithValue("@u", username);
                cmd.Parameters.AddWithValue("@pw", HashPassword(password));
                cmd.Parameters.AddWithValue("@role", role);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@now", DateTime.Now);
                cmd.ExecuteNonQuery();
                return true;
            }
            catch { return false; }
        }

        public bool DeleteUser(int userId)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "DELETE FROM users WHERE id = @id AND username != 'admin'";
                cmd.Parameters.AddWithValue("@id", userId);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch { return false; }
        }

        // ===== 비행 로그 =====
        public void SaveFlightLog(string vehicleId, string name,
            double lat, double lon, double alt,
            double speed, double battery, string status, double heading)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO flight_logs
                        (vehicle_id, name, latitude, longitude, altitude,
                         speed, battery, status, heading, recorded_at)
                    VALUES
                        (@vid, @name, @lat, @lon, @alt,
                         @spd, @bat, @stat, @hdg, @now)";
                cmd.Parameters.AddWithValue("@vid", vehicleId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@lat", lat);
                cmd.Parameters.AddWithValue("@lon", lon);
                cmd.Parameters.AddWithValue("@alt", alt);
                cmd.Parameters.AddWithValue("@spd", speed);
                cmd.Parameters.AddWithValue("@bat", battery);
                cmd.Parameters.AddWithValue("@stat", status);
                cmd.Parameters.AddWithValue("@hdg", heading);
                cmd.Parameters.AddWithValue("@now", DateTime.Now);
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        public List<FlightLogRecord> GetFlightLogs(string vehicleId, int limit = 500)
        {
            var list = new List<FlightLogRecord>();
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT latitude, longitude, altitude, speed, battery,
                       status, heading, recorded_at
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
                    RecordedAt = reader.GetDateTime(7)
                });
            }
            return list;
        }

        // ===== 알림 =====
        public void SaveAlert(string title, string message, string unitId, string severity)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO alerts (title, message, unit_id, severity, occurred_at)
                    VALUES (@title, @msg, @uid, @sev, @now)";
                cmd.Parameters.AddWithValue("@title", title);
                cmd.Parameters.AddWithValue("@msg", message);
                cmd.Parameters.AddWithValue("@uid", unitId);
                cmd.Parameters.AddWithValue("@sev", severity);
                cmd.Parameters.AddWithValue("@now", DateTime.Now);
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        // ===== 오래된 데이터 정리 =====
        public void CleanupOldLogs(int keepDays = 30)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                // 비행 로그 정리
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    DELETE FROM flight_logs
                    WHERE recorded_at < DATE_SUB(NOW(), INTERVAL @days DAY)";
                cmd.Parameters.AddWithValue("@days", keepDays);
                int deleted = cmd.ExecuteNonQuery();

                // 알림 로그 정리 (90일)
                var cmd2 = conn.CreateCommand();
                cmd2.CommandText = @"
                    DELETE FROM alerts
                    WHERE occurred_at < DATE_SUB(NOW(), INTERVAL @days DAY)";
                cmd2.Parameters.AddWithValue("@days", keepDays * 3);
                cmd2.ExecuteNonQuery();
            }
            catch { }
        }

        // ===== 비밀번호 =====
        private static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(
                Encoding.UTF8.GetBytes(password + "AirGuard_Salt_2024"));
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