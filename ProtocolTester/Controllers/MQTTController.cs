using Microsoft.AspNetCore.Mvc;
using ProtocolTester.Models;
using ProtocolTester.Services;

namespace ProtocolTester.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MQTTController : ControllerBase
    {
        private readonly IMQTTTestService _mqttService;
        private readonly ILogger<MQTTController> _logger;

        public MQTTController(IMQTTTestService mqttService, ILogger<MQTTController> logger)
        {
            _mqttService = mqttService;
            _logger = logger;
        }
        /// Test MQTT broker connection
        [HttpPost("test")]
        public async Task<IActionResult> TestConnection([FromBody] MQTTBrokerSettings settings)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _mqttService.TestConnectionAsync(settings);
            return Ok(result);
        }
    }
}