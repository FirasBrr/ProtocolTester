using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using ProtocolTester.Models;
using ProtocolTester.Services;

namespace ProtocolTester.Controllers
{
    public class DevicesController : Controller
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IDeviceTypeService _deviceTypeService;
        private readonly ILogger<DevicesController> _logger;
        private readonly object _lock = new object();

        public DevicesController(
            IWebHostEnvironment webHostEnvironment,
            IDeviceTypeService deviceTypeService,
            ILogger<DevicesController> logger)
        {
            _webHostEnvironment = webHostEnvironment;
            _deviceTypeService = deviceTypeService;
            _logger = logger;
        }

        // ==================== JSON HELPERS ====================

        private string GetJsonFilePath()
        {
            return Path.Combine(_webHostEnvironment.WebRootPath, "data", "modbus_devices.json");
        }

        private List<DeviceJson> LoadDevicesFromJson()
        {
            try
            {
                var filePath = GetJsonFilePath();
                if (!System.IO.File.Exists(filePath))
                {
                    _logger.LogWarning($"Devices file not found at {filePath}");
                    return new List<DeviceJson>();
                }

                var jsonContent = System.IO.File.ReadAllText(filePath);
                var root = JsonSerializer.Deserialize<DeviceRoot>(jsonContent);

                return root?.ModbusDevices ?? new List<DeviceJson>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load from JSON");
                return new List<DeviceJson>();
            }
        }

        private void SaveDevicesToJson(List<DeviceJson> devices)
        {
            try
            {
                var filePath = GetJsonFilePath();
                var existingJson = System.IO.File.ReadAllText(filePath);
                var root = JsonSerializer.Deserialize<DeviceRoot>(existingJson);

                root.ModbusDevices = devices;

                var options = new JsonSerializerOptions { WriteIndented = true };
                var jsonContent = JsonSerializer.Serialize(root, options);

                lock (_lock)
                {
                    System.IO.File.WriteAllText(filePath, jsonContent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save to JSON");
            }
        }

        // ==================== CRUD ACTIONS ====================

        // GET: Devices
        public async Task<IActionResult> Index()
        {
            var devices = LoadDevicesFromJson();
            var deviceTypes = await _deviceTypeService.GetAllDeviceTypesAsync();
            var deviceTypeLookup = new Dictionary<string, string>();

            if (deviceTypes != null && deviceTypes.Any())
            {
                foreach (var d in deviceTypes)
                {
                    if (!string.IsNullOrEmpty(d.Name))
                    {
                        // Use DisplayName if available, otherwise fall back to Name
                        var displayName = !string.IsNullOrEmpty(d.DisplayName) ? d.DisplayName : d.Name;
                        deviceTypeLookup[d.Name] = displayName;
                    }
                }
            }

            ViewBag.DeviceTypeLookup = deviceTypeLookup;
            return View(devices);
        }

        // GET: Devices/Create
        public async Task<IActionResult> Create()
        {
            var deviceTypes = await _deviceTypeService.GetAllDeviceTypesAsync();
            ViewBag.DeviceTypes = deviceTypes;
            return View(new DeviceViewModel());
        }

        // POST: Devices/Create
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] DeviceViewModel model)
        {
            try
            {
                // Verify device type exists
                var deviceType = await _deviceTypeService.GetDeviceTypeByNameAsync(model.DeviceTypeName);
                if (deviceType == null)
                {
                    return BadRequest(new { success = false, message = $"Device type '{model.DeviceTypeName}' not found" });
                }

                var devices = LoadDevicesFromJson();
                var nextId = devices.Any() ? devices.Max(d => d.Id) + 1 : 1;

                var newDevice = new DeviceJson
                {
                    Id = nextId,
                    Name = model.Name,
                    DeviceTypeName = model.DeviceTypeName,
                    Protocol = model.Protocol,
                    ModbusTcpParameters = new ModbusTcpParameters
                    {
                        IpAddress = model.IpAddress,
                        Port = model.Port,
                        RtuOverTcp = false,
                        ConnectionTimeout = model.ConnectionTimeout,
                        SlaveId = model.SlaveId,
                        ReadTimeout = model.ReadTimeout,
                        WriteTimeout = model.WriteTimeout
                    },
                    ModbusSerialParameters = null
                };

                devices.Add(newDevice);
                SaveDevicesToJson(devices);

                return Ok(new
                {
                    success = true,
                    message = $"Device '{model.Name}' created successfully!",
                    device = newDevice
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // GET: Devices/Edit/{id}
        public async Task<IActionResult> Edit(int id)
        {
            var devices = LoadDevicesFromJson();
            var device = devices.FirstOrDefault(d => d.Id == id);

            if (device == null)
            {
                return NotFound();
            }

            var deviceTypes = await _deviceTypeService.GetAllDeviceTypesAsync();
            ViewBag.DeviceTypes = deviceTypes;

            var viewModel = new DeviceViewModel
            {
                Id = device.Id,
                Name = device.Name,
                DeviceTypeName = device.DeviceTypeName,
                Protocol = device.Protocol,
                IpAddress = device.ModbusTcpParameters?.IpAddress ?? "",
                Port = device.ModbusTcpParameters?.Port ?? 502,
                SlaveId = device.ModbusTcpParameters?.SlaveId ?? device.ModbusSerialParameters?.SlaveId ?? 1,
                ConnectionTimeout = device.ModbusTcpParameters?.ConnectionTimeout ?? 3000,
                ReadTimeout = device.ModbusTcpParameters?.ReadTimeout ?? device.ModbusSerialParameters?.ReadTimeout ?? 1000,
                WriteTimeout = device.ModbusTcpParameters?.WriteTimeout ?? device.ModbusSerialParameters?.WriteTimeout ?? 1500
            };

            return View(viewModel);
        }

        // POST: Devices/Edit
        [HttpPost]
        public async Task<IActionResult> Edit([FromBody] DeviceViewModel model)
        {
            try
            {
                // Verify device type exists
                var deviceType = await _deviceTypeService.GetDeviceTypeByNameAsync(model.DeviceTypeName);
                if (deviceType == null)
                {
                    return BadRequest(new { success = false, message = $"Device type '{model.DeviceTypeName}' not found" });
                }

                var devices = LoadDevicesFromJson();
                var existing = devices.FirstOrDefault(d => d.Id == model.Id);

                if (existing == null)
                {
                    return NotFound(new { success = false, message = "Device not found" });
                }

                existing.Name = model.Name;
                existing.DeviceTypeName = model.DeviceTypeName;
                existing.Protocol = model.Protocol;

                if (existing.ModbusTcpParameters != null)
                {
                    existing.ModbusTcpParameters.IpAddress = model.IpAddress;
                    existing.ModbusTcpParameters.Port = model.Port;
                    existing.ModbusTcpParameters.SlaveId = model.SlaveId;
                    existing.ModbusTcpParameters.ConnectionTimeout = model.ConnectionTimeout;
                    existing.ModbusTcpParameters.ReadTimeout = model.ReadTimeout;
                    existing.ModbusTcpParameters.WriteTimeout = model.WriteTimeout;
                }
                else
                {
                    existing.ModbusTcpParameters = new ModbusTcpParameters
                    {
                        IpAddress = model.IpAddress,
                        Port = model.Port,
                        RtuOverTcp = false,
                        ConnectionTimeout = model.ConnectionTimeout,
                        SlaveId = model.SlaveId,
                        ReadTimeout = model.ReadTimeout,
                        WriteTimeout = model.WriteTimeout
                    };
                }

                SaveDevicesToJson(devices);

                return Ok(new
                {
                    success = true,
                    message = $"Device '{model.Name}' updated successfully!"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // DELETE: Devices/Delete/{id}
        [HttpDelete]
        public IActionResult Delete(int id)
        {
            var devices = LoadDevicesFromJson();
            var device = devices.FirstOrDefault(d => d.Id == id);

            if (device == null)
            {
                return NotFound(new { success = false, message = "Device not found" });
            }

            devices.Remove(device);
            SaveDevicesToJson(devices);

            return Ok(new { success = true, message = $"Device '{device.Name}' deleted successfully!" });
        }

        // ==================== API ENDPOINTS ====================

        // GET: API endpoint for device types
        [HttpGet("api/devicetypes")]
        public async Task<IActionResult> GetDeviceTypes()
        {
            var types = await _deviceTypeService.GetAllDeviceTypesAsync();
            return Ok(types);
        }
        // GET: API endpoint for full device type info
        [HttpGet("api/devicetypes/{name}")]
        public async Task<IActionResult> GetDeviceType(string name)
        {
            var deviceType = await _deviceTypeService.GetDeviceTypeByNameAsync(name);
            if (deviceType == null)
            {
                return NotFound(new { message = $"Device type '{name}' not found" });
            }

            return Ok(deviceType);
        }

        // GET: API endpoint for device type mappings
        [HttpGet("api/devicetypes/{name}/mappings")]
        public async Task<IActionResult> GetDeviceTypeMappings(string name)
        {
            var deviceType = await _deviceTypeService.GetDeviceTypeByNameAsync(name);
            if (deviceType == null)
            {
                return NotFound(new { message = $"Device type '{name}' not found" });
            }

            return Ok(deviceType.Mappings);
        }

        // GET: API endpoint for all devices
        [HttpGet("api/devices")]
        public IActionResult GetDevices()
        {
            var devices = LoadDevicesFromJson();
            return Ok(devices);
        }

        // GET: API endpoint for single device with test register
        [HttpGet("api/devices/{id}/testregister")]
        public async Task<IActionResult> GetDeviceTestRegister(int id)
        {
            var devices = LoadDevicesFromJson();
            var device = devices.FirstOrDefault(d => d.Id == id);

            if (device == null)
            {
                return NotFound(new { message = "Device not found" });
            }

            // Get the device type to find the first mapping
            var deviceType = await _deviceTypeService.GetDeviceTypeByNameAsync(device.DeviceTypeName);
            if (deviceType == null || deviceType.Mappings == null || !deviceType.Mappings.Any())
            {
                return Ok(new { testRegister = 40001 }); // Default fallback
            }

            // Get the first mapping's StartAddress as the test register
            var testRegister = deviceType.Mappings.First().StartAddress;

            return Ok(new
            {
                testRegister = testRegister,
                mappingName = deviceType.Mappings.First().Name,
                deviceTypeName = device.DeviceTypeName
            });
        }
    }
}