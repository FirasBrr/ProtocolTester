using ProtocolTester.Models;

namespace ProtocolTester.Services
{
    public interface IDeviceTypeService
    {
        Task<List<DeviceType>> GetAllDeviceTypesAsync();
        Task<DeviceType> GetDeviceTypeByNameAsync(string name);
        Task<List<string>> GetDeviceTypeNamesAsync();
    }
}