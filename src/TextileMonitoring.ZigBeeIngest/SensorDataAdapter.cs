
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TextileMonitoring.Infrastructure.Messaging;
using TextileMonitoring.Messages.Events;
using TextileMonitoring.ZigBeeIngest.Data;
using Serilog;
using ILogger = Serilog.ILogger;

namespace TextileMonitoring.ZigBeeIngest
{
    public interface ISensorDataAdapter
    {
        ISensorDataReceived? ParseDustPayload(byte[] data, IPEndPoint remote);
        ISensorDataReceived? ParseFungiPayload(byte[] data, IPEndPoint remote);
    }

    public class SensorDataAdapter : ISensorDataAdapter
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;

        public SensorDataAdapter(IServiceProvider serviceProvider, ILogger logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public ISensorDataReceived? ParseDustPayload(byte[] data, IPEndPoint remote)
        {
            try
            {
                if (data.Length < 40)
                    throw new ArgumentException("Dust packet too short");

                var sensorCode = Encoding.ASCII.GetString(data, 0, 7).Trim('\0', ' ');
                var payloadType = data[7];
                if (payloadType != 0x01) return null;

                var timestamp = DateTime.UnixEpoch.AddSeconds(BitConverter.ToUInt32(data, 10));
                var temperature = BitConverter.ToSingle(data, 26);
                var humidity = BitConverter.ToSingle(data, 30);

                var result = new SensorDataReceived
                {
                    CorrelationId = Guid.NewGuid(),
                    Timestamp = timestamp,
                    SensorCode = sensorCode,
                    SensorType = SensorType.DustSensor,
                    PM2_5 = (decimal)Math.Round(BitConverter.ToSingle(data, 14), 2),
                    PM10 = (decimal)Math.Round(BitConverter.ToSingle(data, 18), 2),
                    FrassDensity = (decimal)Math.Round(BitConverter.ToSingle(data, 22), 4),
                    Temperature = Math.Round((decimal)temperature, 1),
                    Humidity = Math.Round((decimal)humidity, 1),
                    HoleCount = BitConverter.ToInt32(data, 34),
                    SignalStrength = BitConverter.ToInt16(data, 38)
                };

                var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var sensor = dbContext.Sensors
                    .AsNoTracking()
                    .FirstOrDefault(s => s.SensorCode == sensorCode && s.SensorType == (int)Models.SensorType.DustSensor && s.IsActive);

                if (sensor == null)
                {
                    _logger.Warning("Unknown dust sensor: {Code}", sensorCode);
                    return null;
                }

                result.TextileId = sensor.TextileId;
                var textile = dbContext.Textiles.Find(sensor.TextileId);
                var area = textile?.AreaCm2 > 0 ? textile.AreaCm2 : 1000m;
                result.HoleDensity = Math.Round((decimal)result.HoleCount.Value / area * 100m, 4);

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to parse dust payload from {Remote}", remote);
                return null;
            }
        }

        public ISensorDataReceived? ParseFungiPayload(byte[] data, IPEndPoint remote)
        {
            try
            {
                if (data.Length < 68)
                    throw new ArgumentException("Fungi packet too short");

                var sensorCode = Encoding.ASCII.GetString(data, 0, 7).Trim('\0', ' ');
                var payloadType = data[7];
                if (payloadType != 0x02) return null;

                var timestamp = DateTime.UnixEpoch.AddSeconds(BitConverter.ToUInt32(data, 10));
                var temperature = BitConverter.ToSingle(data, 30);
                var humidity = BitConverter.ToSingle(data, 34);
                var fungiTypeBytes = new byte[32];
                Array.Copy(data, 38, fungiTypeBytes, 0, 32);
                var dominantType = Encoding.UTF8.GetString(fungiTypeBytes).TrimEnd('\0');

                var result = new SensorDataReceived
                {
                    CorrelationId = Guid.NewGuid(),
                    Timestamp = timestamp,
                    SensorCode = sensorCode,
                    SensorType = SensorType.FungiSensor,
                    SporeCount = Math.Round(BitConverter.ToDouble(data, 14), 2),
                    FungiCFU = Math.Round((decimal)BitConverter.ToDouble(data, 22), 2),
                    Temperature = Math.Round((decimal)temperature, 1),
                    Humidity = Math.Round((decimal)humidity, 1),
                    DominantFungiType = dominantType,
                    SignalStrength = BitConverter.ToInt16(data, 66)
                };

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var sensor = dbContext.Sensors
                    .AsNoTracking()
                    .FirstOrDefault(s => s.SensorCode == sensorCode && s.SensorType == (int)Models.SensorType.FungiSensor && s.IsActive);

                if (sensor == null)
                {
                    _logger.Warning("Unknown fungi sensor: {Code}", sensorCode);
                    return null;
                }

                result.TextileId = sensor.TextileId;
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to parse fungi payload from {Remote}", remote);
                return null;
            }
        }
    }
}
