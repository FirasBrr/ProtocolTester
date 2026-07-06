using System.Text.Json;
using ProtocolTester.Models;

namespace ProtocolTester.Services
{
    public class DeviceTypeService : IDeviceTypeService
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<DeviceTypeService> _logger;
        private List<DeviceType> _deviceTypes = new();

        public DeviceTypeService(IWebHostEnvironment webHostEnvironment, ILogger<DeviceTypeService> logger)
        {
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
            LoadDeviceTypes();
        }

        private void LoadDeviceTypes()
        {
            try
            {
                var deviceTypesPath = Path.Combine(_webHostEnvironment.WebRootPath, "data", "modbus_types");

                if (!Directory.Exists(deviceTypesPath))
                {
                    _logger.LogWarning($"Device types folder not found at {deviceTypesPath}");
                    return;
                }

                var jsonFiles = Directory.GetFiles(deviceTypesPath, "*.dev.json");


                _deviceTypes = new List<DeviceType>();

                foreach (var file in jsonFiles)
                {
                    try
                    {
                        var jsonContent = System.IO.File.ReadAllText(file);
                        var deviceType = JsonSerializer.Deserialize<DeviceType>(jsonContent);
                        if (deviceType != null)
                        {
                            _deviceTypes.Add(deviceType);
                            _logger.LogInformation($"Loaded device type: {deviceType.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to load device type from {file}");
                    }
                }

                _logger.LogInformation($"Loaded {_deviceTypes.Count} device types");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load device types");
            }
        }

        public async Task<List<DeviceType>> GetAllDeviceTypesAsync()
        {
            return await Task.FromResult(_deviceTypes);
        }

        public async Task<DeviceType> GetDeviceTypeByNameAsync(string name)
        {
            return await Task.FromResult(_deviceTypes.FirstOrDefault(d => d.Name == name));
        }

        public async Task<List<string>> GetDeviceTypeNamesAsync()
        {
            return await Task.FromResult(_deviceTypes.Select(d => d.Name).ToList());
        }
    }
}