using System.ComponentModel.DataAnnotations;

namespace ProtocolTester.Models
{
    public class DeviceViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Device name is required")]
        [Display(Name = "Device Name")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Device type is required")]
        [Display(Name = "Device Type")]
        public string DeviceTypeName { get; set; }

        [Display(Name = "Protocol")]
        public int Protocol { get; set; } 

        // TCP Parameters
        [Required(ErrorMessage = "IP Address is required")]
        [Display(Name = "IP Address")]
        public string IpAddress { get; set; }

        [Required(ErrorMessage = "Port is required")]
        [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
        [Display(Name = "Port")]
        public int Port { get; set; } 

        [Required(ErrorMessage = "Slave ID is required")]
        [Range(1, 247, ErrorMessage = "Slave ID must be between 1 and 247")]
        [Display(Name = "Slave ID")]
        public int SlaveId { get; set; } 

        [Range(100, 30000, ErrorMessage = "Connection timeout must be between 100 and 30000 ms")]
        [Display(Name = "Connection Timeout (ms)")]
        public int ConnectionTimeout { get; set; } 

        [Range(100, 30000, ErrorMessage = "Read timeout must be between 100 and 30000 ms")]
        [Display(Name = "Read Timeout (ms)")]
        public int ReadTimeout { get; set; }

        [Range(100, 30000, ErrorMessage = "Write timeout must be between 100 and 30000 ms")]
        [Display(Name = "Write Timeout (ms)")]
        public int WriteTimeout { get; set; } 

     

        // Test Results
        public bool? IsConnected { get; set; }
        public string TestResultMessage { get; set; }
        public int? ResponseTimeMs { get; set; }
        public object TestValue { get; set; }
        public List<DiagnosticCheck> Diagnostics { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastTestedAt { get; set; }
        public bool? LastTestResult { get; set; }
    }
}