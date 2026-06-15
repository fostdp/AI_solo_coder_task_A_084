
namespace ZigBeeIngest.Worker;

public class RabbitMqSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public ushort PrefetchCount { get; set; } = 16;
    public int RetryCount { get; set; } = 3;
    public int RetryIntervalMs { get; set; } = 1000;
}

public class UdpSettings
{
    public int ListenPort { get; set; } = 8684;
    public int ReceiveTimeoutMs { get; set; } = 5000;
    public string MulticastGroup { get; set; } = "239.255.86.84";
    public bool EnableMulticast { get; set; } = true;
}

public class BatchingSettings
{
    public int BatchSize { get; set; } = 50;
    public int FlushIntervalMs { get; set; } = 10000;
}
