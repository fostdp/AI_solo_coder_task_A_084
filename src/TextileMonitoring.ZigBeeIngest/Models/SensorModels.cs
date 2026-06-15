
namespace TextileMonitoring.ZigBeeIngest.Models
{
    public enum SensorType
    {
        DustSensor = 1,
        FungiSensor = 2
    }

    public class Sensor
    {
        public int Id { get; set; }
        public string SensorCode { get; set; } = string.Empty;
        public int TextileId { get; set; }
        public int SensorType { get; set; }
        public bool IsActive { get; set; }
    }

    public class Textile
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal WidthCm { get; set; }
        public decimal HeightCm { get; set; }
        public decimal AreaCm2 { get; set; }
    }
}
