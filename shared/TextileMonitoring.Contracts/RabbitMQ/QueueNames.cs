
namespace TextileMonitoring.Contracts.RabbitMQ;

public static class QueueNames
{
    public const string SensorData = "textile.sensor_data";
    public const string PopulationPrediction = "textile.population_prediction";
    public const string MildewPrediction = "textile.mildew_prediction";
    public const string AlertTrigger = "textile.alert_trigger";
    public const string AlertDispatch = "textile.alert_dispatch";

    public static class Exchanges
    {
        public const string Sensor = "textile.exchange.sensor";
        public const string Prediction = "textile.exchange.prediction";
        public const string Alert = "textile.exchange.alert";
    }

    public static class RoutingKeys
    {
        public const string DustSensor = "sensor.dust";
        public const string FungiSensor = "sensor.fungi";
        public const string Population = "prediction.population";
        public const string Mildew = "prediction.mildew";
        public const string Synergy = "prediction.synergy";
    }
}
