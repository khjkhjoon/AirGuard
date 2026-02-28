using System;

namespace AirGuard.WPF.Models
{
    public class VehicleData
    {
        public string VehicleId { get; set; } = "";
        public string Name { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public double Speed { get; set; }
        public double Battery { get; set; }
        public string Status { get; set; } = "Idle";
        public double Heading { get; set; }
        public DateTime Timestamp { get; set; }
    }
}