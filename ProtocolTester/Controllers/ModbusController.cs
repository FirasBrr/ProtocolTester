using Microsoft.AspNetCore.Mvc;
using ProtocolTester.Models;
using ProtocolTester.Services;

namespace ProtocolTester.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ModbusController : ControllerBase
    {
        private readonly IModbusTestService _modbusService;

        public ModbusController(IModbusTestService modbusService)
        {
            _modbusService = modbusService;
        }

        /// Test Modbus TCP connectivity
        [HttpPost("tcp/test")]
        public async Task<IActionResult> TestTcp([FromBody] ModbusTcpRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _modbusService.TestTcpAsync(request);
            return Ok(result);
        }

    }
}
