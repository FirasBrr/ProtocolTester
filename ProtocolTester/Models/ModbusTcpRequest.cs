using System.ComponentModel.DataAnnotations;

namespace ProtocolTester.Models
{
    public class ModbusTcpRequest
    {
        [Required]
        public string IpAddress { get; set; }

        [Range(1, 65535)]
        public int Port { get; set; }

        [Range(1, 247)]
        public int SlaveId { get; set; }
        public string DataType { get; set; } 

        public int TestRegister { get; set; }

        public int TimeoutMs { get; set; }
        public int? ConnectionTimeout { get; set; }

    }


}
