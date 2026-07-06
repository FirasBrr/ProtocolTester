using ProtocolTester.Models;

namespace ProtocolTester.Services
{
    public interface IMQTTTestService
    {
        Task<MQTTBrokerSettings> TestConnectionAsync(MQTTBrokerSettings settings);

    }
}
