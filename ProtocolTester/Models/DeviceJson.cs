namespace ProtocolTester.Models
{
    

    public class ModbusTcpParameters
    {
        public string IpAddress { get; set; }
        public int Port { get; set; }
        public bool RtuOverTcp { get; set; }
        public int ConnectionTimeout { get; set; }
        public int SlaveId { get; set; }
        public int ReadTimeout { get; set; }
        public int WriteTimeout { get; set; }
    }

    public class ModbusSerialParameters
    {
        public string SerialInterface { get; set; }
        public string ComPort { get; set; }
        public int BaudRate { get; set; }
        public int DataBits { get; set; }
        public string Parity { get; set; }
        public string StopBits { get; set; }
        public string Mode { get; set; }
        public int SlaveId { get; set; }
        public int ReadTimeout { get; set; }
        public int WriteTimeout { get; set; }
    }

    public class DeviceJson
    {
        public string Name { get; set; }
        public int Id { get; set; }
        public string DeviceTypeName { get; set; }
        public ModbusTcpParameters ModbusTcpParameters { get; set; }
        public ModbusSerialParameters ModbusSerialParameters { get; set; }
        public int Protocol { get; set; } 
    }

    public class DeviceRoot
    {
        public string Directory { get; set; }
        public string DeviceTypeFileLocation { get; set; }
        public List<DeviceJson> ModbusDevices { get; set; }
    }
}