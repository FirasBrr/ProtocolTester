using System.Diagnostics;
using System.Net.Sockets;
using ProtocolTester.Models;

namespace ProtocolTester.Services
{
    public class ModbusTestService : IModbusTestService
    {
        private readonly ILogger<ModbusTestService> _logger;

        public ModbusTestService(ILogger<ModbusTestService> logger)
        {
            _logger = logger;
        }

        public async Task<TestResponse> TestTcpAsync(ModbusTcpRequest request)
        {
            var stopwatch = Stopwatch.StartNew();
            var diagnostics = new List<DiagnosticCheck>();

            try
            {
                _logger.LogInformation($"Testing TCP: {request.IpAddress}:{request.Port}, Slave: {request.SlaveId}, DataType: {request.DataType}");

                // Step 1: TCP connection test
                var tcpResult = await TestTcpConnection(request);
                diagnostics.Add(tcpResult);
                if (!tcpResult.IsPassed)
                    return CreateResponse(false, "Network test failed", stopwatch, diagnostics);

                // Step 2: Modbus + Register test with dynamic quantity based on DataType
                var (modbusResult, registerResult) = await TestModbusAndRegisterReal(request);
                diagnostics.Add(modbusResult);
                if (registerResult != null)
                    diagnostics.Add(registerResult);

                if (!modbusResult.IsPassed)
                    return CreateResponse(false, "Modbus test failed", stopwatch, diagnostics);

                return new TestResponse
                {
                    IsSuccessful = true,
                    Message = "✅ Device is connected and responding!",
                    ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
                    TestValue = registerResult is { IsPassed: true } ? registerResult.Message : null,
                    Diagnostics = diagnostics,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TCP test failed");
                return new TestResponse
                {
                    IsSuccessful = false,
                    Message = "❌ Test failed with exception",
                    ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
                    ErrorDetails = ex.Message,
                    Diagnostics = diagnostics,
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        // ==================== PRIVATE HELPERS ====================

        private async Task<DiagnosticCheck> TestTcpConnection(ModbusTcpRequest request)
        {
            try
            {
                using var client = new TcpClient();
                var timeoutMs = request.ConnectionTimeout ?? request.TimeoutMs;
                var connectTask = client.ConnectAsync(request.IpAddress, request.Port);
                var timeout = Task.Delay(timeoutMs);

                if (await Task.WhenAny(connectTask, timeout) == connectTask)
                {
                    await connectTask;
                    return new DiagnosticCheck
                    {
                        Name = "Network",
                        IsPassed = true,
                        Message = $"Connected to {request.IpAddress}:{request.Port}",
                        Details = $"Connection established in < {timeoutMs}ms"
                    };
                }

                return new DiagnosticCheck
                {
                    Name = "Network",
                    IsPassed = false,
                    Message = $"Connection timeout after {timeoutMs}ms",
                    Suggestion = "Device may be powered off, IP incorrect, or port blocked by firewall"
                };
            }
            catch (SocketException ex)
            {
                return ex.SocketErrorCode switch
                {
                    SocketError.ConnectionRefused => new DiagnosticCheck
                    {
                        Name = "Network",
                        IsPassed = false,
                        Message = $"Connection refused by {request.IpAddress}:{request.Port}",
                        Suggestion = "Device may have reached max connections, or IP is restricted"
                    },
                    SocketError.HostUnreachable or SocketError.NetworkUnreachable => new DiagnosticCheck
                    {
                        Name = "Network",
                        IsPassed = false,
                        Message = $"Host unreachable ({request.IpAddress}:{request.Port})",
                        Suggestion = "Check network cable, routing, and that the device is powered on"
                    },
                    SocketError.TimedOut => new DiagnosticCheck
                    {
                        Name = "Network",
                        IsPassed = false,
                        Message = $"Socket timed out connecting to {request.IpAddress}:{request.Port}",
                        Suggestion = "Check network connectivity and firewall"
                    },
                    _ => new DiagnosticCheck
                    {
                        Name = "Network",
                        IsPassed = false,
                        Message = $"Socket error [{ex.SocketErrorCode}]: {ex.Message}",
                        Suggestion = "Check network cable and firewall"
                    }
                };
            }
            catch (Exception ex)
            {
                return new DiagnosticCheck
                {
                    Name = "Network",
                    IsPassed = false,
                    Message = $"Network error: {ex.Message}",
                    Suggestion = "Check network cable and firewall"
                };
            }
        }

        /// <summary>
        /// Tests Modbus and Register with dynamic quantity based on DataType.
        /// Uses the mapping's DataType to determine how many registers to read.
        /// </summary>
        private async Task<(DiagnosticCheck ModbusCheck, DiagnosticCheck? RegisterCheck)> TestModbusAndRegisterReal(ModbusTcpRequest request)
        {
            try
            {
                using var client = new TcpClient();

                var connectTimeoutMs = request.ConnectionTimeout ?? request.TimeoutMs;
                using (var connectCts = new CancellationTokenSource(connectTimeoutMs))
                {
                    try
                    {
                        await client.ConnectAsync(request.IpAddress, request.Port, connectCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        var timeoutCheck = new DiagnosticCheck
                        {
                            Name = "Modbus",
                            IsPassed = false,
                            Message = $"Connection to device timed out after {connectTimeoutMs}ms",
                            Suggestion = "Device may only accept one client at a time, or is otherwise unreachable for a second connection"
                        };
                        return (timeoutCheck, null);
                    }
                }

                var stream = client.GetStream();

                // ✅ DYNAMIC: Determine register count based on DataType
                int registerCount = GetRegisterCount(request.DataType);

                // ✅ Use the correct function code based on DataType/RegisterType
                byte functionCode = GetFunctionCode(request.DataType);

                byte[] modbusRequest = BuildModbusRequest(request.SlaveId, request.TestRegister, registerCount, functionCode);
                await stream.WriteAsync(modbusRequest, 0, modbusRequest.Length);

                byte[] response;
                try
                {
                    using var readCts = new CancellationTokenSource(request.TimeoutMs);
                    response = await ReadFullModbusResponseAsync(stream, readCts.Token);
                }
                catch (OperationCanceledException)
                {
                    var timeoutCheck = new DiagnosticCheck
                    {
                        Name = "Modbus",
                        IsPassed = false,
                        Message = $"Modbus timeout after {request.TimeoutMs}ms",
                        Suggestion = "Check if Slave ID is correct and device is responding"
                    };
                    return (timeoutCheck, null);
                }

                if (response.Length < 9)
                {
                    var shortCheck = new DiagnosticCheck
                    {
                        Name = "Modbus",
                        IsPassed = false,
                        Message = "Invalid response (too short)",
                        Suggestion = "Check device is responding correctly"
                    };
                    return (shortCheck, null);
                }

                // Transaction ID mismatch
                if (response[0] != modbusRequest[0] || response[1] != modbusRequest[1])
                {
                    var mismatchCheck = new DiagnosticCheck
                    {
                        Name = "Modbus",
                        IsPassed = false,
                        Message = "Transaction ID mismatch",
                        Suggestion = "Response belongs to a different request — check for stale data"
                    };
                    return (mismatchCheck, null);
                }

                // Protocol ID mismatch
                if (response[2] != 0x00 || response[3] != 0x00)
                {
                    var protoCheck = new DiagnosticCheck
                    {
                        Name = "Modbus",
                        IsPassed = false,
                        Message = "Invalid Protocol ID in response",
                        Suggestion = "Peer may not be a Modbus TCP device"
                    };
                    return (protoCheck, null);
                }

                // Unit ID (Slave ID) validation
                if (response[6] != request.SlaveId)
                {
                    var slaveIdCheck = new DiagnosticCheck
                    {
                        Name = "Modbus",
                        IsPassed = false,
                        Message = $"Slave ID mismatch: expected {request.SlaveId}, got {response[6]}",
                        Suggestion = "Response came from wrong device. Check gateway routing."
                    };
                    return (slaveIdCheck, null);
                }

                // Check for Modbus exception
                byte functionCodeResponse = response[7];
                if ((functionCodeResponse & 0x80) != 0)
                {
                    byte exceptionCode = response[8];
                    var modbusExceptionCheck = BuildModbusExceptionCheck("Modbus", exceptionCode);
                    var registerExceptionCheck = BuildModbusExceptionCheck("Register", exceptionCode);
                    return (modbusExceptionCheck, registerExceptionCheck);
                }

                var modbusOkCheck = new DiagnosticCheck
                {
                    Name = "Modbus",
                    IsPassed = true,
                    Message = $"Slave {request.SlaveId} is responding"
                };

                // ✅ DYNAMIC: Parse the value based on DataType and register count
                if (response.Length >= 9 + (registerCount * 2))
                {
                    object parsedValue = ParseRegisterValue(response, registerCount, request.DataType);
                    var registerOkCheck = new DiagnosticCheck
                    {
                        Name = "Register",
                        IsPassed = true,
                        Message = $"{parsedValue}"
                    };
                    return (modbusOkCheck, registerOkCheck);
                }

                var noDataCheck = new DiagnosticCheck
                {
                    Name = "Register",
                    IsPassed = false,
                    Message = "No data in response",
                    Suggestion = "Check register address"
                };
                return (modbusOkCheck, noDataCheck);
            }
            catch (Exception ex)
            {
                var errorCheck = new DiagnosticCheck
                {
                    Name = "Modbus",
                    IsPassed = false,
                    Message = $"Modbus error: {ex.Message}",
                    Suggestion = "Check Slave ID and Modbus settings"
                };
                return (errorCheck, null);
            }
        }

        // ==================== DYNAMIC REGISTER HANDLING ====================

        /// <summary>
        /// Determines the number of registers needed based on DataType.
        /// </summary>
        public static int GetRegisterCount(string dataType)
        {
            if (string.IsNullOrEmpty(dataType))
                throw new ArgumentException("DataType cannot be null or empty");

            return dataType.ToLower() switch
            {
                // 16-bit types → 1 register
                "short" or "int16" or "ushort" or "uint16" or "word" or "bool" or "boolean" => 1,

                // 32-bit types → 2 registers
                "int" or "integer" or "uint" or "uinteger" or
                "float" or "single" or "real" or "dword" or "float32" => 2,

                // 64-bit types → 4 registers
                "long" or "int64" or "ulong" or "uint64" or
                "double" or "float64" => 4,

                // String / raw (variable length)
                "string" or "raw" => 1,

                _ => throw new NotSupportedException($"Unsupported DataType: {dataType}")
            };
        }

        /// <summary>
        /// Determines the Modbus function code based on DataType/RegisterType.
        /// </summary>
        public static byte GetFunctionCode(string dataType)
        {
            if (string.IsNullOrEmpty(dataType))
                throw new ArgumentException("DataType cannot be null or empty");

            return dataType.ToLower() switch
            {
                // Coils (1-bit values)
                "bool" or "boolean" or "coil" => 0x01,

                // Discrete Inputs (1-bit read-only)
                "discrete" or "discreteinput" => 0x02,

                // Holding Registers (16-bit read/write)
                "short" or "int16" or "ushort" or "uint16" or "word" or
                "int" or "integer" or "uint" or "uinteger" or
                "float" or "single" or "real" or "dword" or "float32" or
                "long" or "int64" or "ulong" or "uint64" or
                "double" or "float64" or "string" or "raw" => 0x03,

                // Input Registers (16-bit read-only)
                "input" or "inputregister" => 0x04,

                _ => 0x03 // Default to Holding Register
            };
        }

        /// <summary>
        /// Parses register value based on DataType and byte count.
        /// </summary>
        public static object ParseRegisterValue(byte[] response, int registerCount, string dataType)
        {
            if (string.IsNullOrEmpty(dataType))
                throw new ArgumentException("DataType cannot be null or empty");

            // Extract bytes from response (start at byte 9, length = registerCount * 2)
            byte[] valueBytes = new byte[registerCount * 2];
            Array.Copy(response, 9, valueBytes, 0, registerCount * 2);

            return dataType.ToLower() switch
            {
                // Boolean/Coil (1 bit)
                "bool" or "boolean" or "coil" => valueBytes[0] != 0,

                // 16-bit types (1 register)
                "short" or "int16" => BitConverter.ToInt16(valueBytes, 0),
                "ushort" or "uint16" => BitConverter.ToUInt16(valueBytes, 0),

                // 32-bit types (2 registers)
                "int" or "integer" => BitConverter.ToInt32(ReverseIfLittleEndian(valueBytes)),
                "uint" or "uinteger" => BitConverter.ToUInt32(ReverseIfLittleEndian(valueBytes)),
                "float" or "single" or "real" or "float32" => BitConverter.ToSingle(ReverseIfLittleEndian(valueBytes)),

                // 64-bit types (4 registers)
                "long" or "int64" => BitConverter.ToInt64(ReverseIfLittleEndian(valueBytes)),
                "ulong" or "uint64" => BitConverter.ToUInt64(ReverseIfLittleEndian(valueBytes)),
                "double" or "float64" => BitConverter.ToDouble(ReverseIfLittleEndian(valueBytes)),

                // String
                "string" => System.Text.Encoding.ASCII.GetString(valueBytes).TrimEnd('\0'),

                // Raw bytes
                "raw" => valueBytes,

                _ => throw new NotSupportedException($"Unsupported DataType: {dataType}")
            };
        }

        /// <summary>
        /// Modbus is Big Endian. If the system is Little Endian, reverse the bytes.
        /// </summary>
        private static byte[] ReverseIfLittleEndian(byte[] bytes)
        {
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return bytes;
        }

        /// <summary>
        /// Builds a Modbus request with dynamic quantity and function code.
        /// </summary>
        private static byte[] BuildModbusRequest(int slaveId, int register, int quantity, byte functionCode)
        {
            return new byte[12]
            {
                0x00, 0x01,                         // Transaction ID
                0x00, 0x00,                         // Protocol ID
                0x00, 0x06,                         // Length (6 bytes follow)
                (byte)slaveId,                      // Unit ID
                functionCode,                       // Function Code (0x01, 0x02, 0x03, 0x04)
                (byte)(register >> 8),              // Register address Hi
                (byte)(register & 0xFF),            // Register address Lo
                (byte)(quantity >> 8),              // Quantity Hi
                (byte)(quantity & 0xFF)             // Quantity Lo
            };
        }

        // ==================== RESPONSE READING ====================

        private static async Task<byte[]> ReadFullModbusResponseAsync(NetworkStream stream, CancellationToken token)
        {
            var buffer = new byte[260];
            int totalRead = 0;

            while (totalRead < 6)
            {
                int n = await stream.ReadAsync(buffer.AsMemory(totalRead, 6 - totalRead), token);
                if (n == 0) throw new IOException("Connection closed before MBAP header was fully received");
                totalRead += n;
            }

            int mbapLength = (buffer[4] << 8) | buffer[5];
            int expectedTotal = 6 + mbapLength;

            if (expectedTotal > buffer.Length)
                expectedTotal = buffer.Length;

            while (totalRead < expectedTotal)
            {
                int n = await stream.ReadAsync(buffer.AsMemory(totalRead, expectedTotal - totalRead), token);
                if (n == 0) break;
                totalRead += n;
            }

            return buffer[..totalRead];
        }

        // ==================== SHARED UTILITIES ====================

        private static DiagnosticCheck BuildModbusExceptionCheck(string checkName, byte exceptionCode)
        {
            var (message, suggestion) = exceptionCode switch
            {
                0x01 => ("Illegal Function — function code not supported by this device",
                         "Verify the device supports this function code"),
                0x02 => ("Illegal Data Address — register does not exist on this device",
                         "Check the register address; it may be out of range for this device"),
                0x03 => ("Illegal Data Value — quantity or data field out of bounds",
                         "Ensure you are requesting a valid number of registers (1–125)"),
                0x04 => ("Server Device Failure — unrecoverable error in the slave device",
                         "Inspect the device for hardware faults or restart it"),
                0x05 => ("Acknowledge — device accepted the request but needs more time",
                         "This is expected for long-running commands; poll again shortly"),
                0x06 => ("Server Device Busy — device is processing a long-running command",
                         "Wait briefly and retry; reduce poll frequency if this recurs"),
                0x07 => ("Negative Acknowledge — device cannot perform the requested function",
                         "Check that the requested operation is supported in the device's current mode"),
                0x08 => ("Memory Parity Error — parity error reading extended memory",
                         "Check the device's extended file/memory area for corruption"),
                0x0A => ("Gateway Path Unavailable — Modbus gateway is misconfigured or overloaded",
                         "Check the gateway configuration and network load"),
                0x0B => ("Gateway Target Device Failed to Respond — downstream RTU device is off or wrong Slave ID",
                         "Verify the RTU device is powered on and that the Slave ID matches"),
                _ => ($"Unknown Modbus exception (Code: 0x{exceptionCode:X2})",
                         "Consult the device manual for vendor-specific exception codes")
            };

            return new DiagnosticCheck
            {
                Name = checkName,
                IsPassed = false,
                Message = $"Modbus exception 0x{exceptionCode:X2}: {message}",
                Suggestion = suggestion
            };
        }

        private TestResponse CreateResponse(bool success, string message, Stopwatch stopwatch, List<DiagnosticCheck> diagnostics)
        {
            return new TestResponse
            {
                IsSuccessful = success,
                Message = success ? $"✅ {message}" : $"❌ {message}",
                ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
                Diagnostics = diagnostics,
                Timestamp = DateTime.UtcNow
            };
        }
    }
}