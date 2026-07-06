using ProtocolTester.Models;

namespace ProtocolTester.Services
{
    public interface IModbusTestService
    {
        // TCP
        Task<TestResponse> TestTcpAsync(ModbusTcpRequest request);

       
    }
}
