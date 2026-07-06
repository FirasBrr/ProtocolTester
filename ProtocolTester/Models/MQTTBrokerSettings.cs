using System.ComponentModel.DataAnnotations;

namespace ProtocolTester.Models
{
    public class MQTTBrokerSettings
    {
        // ==================== JUG 25 MQTT SETTINGS ====================

        [Display(Name = "MQTT Version")]
        public string MqttVersion { get; set; } = "MQTTVERSION_DEFAULT";

        [Required(ErrorMessage = "Broker address is required")]
        [Display(Name = "Broker Address")]
        public string BrokerAddress { get; set; }

        [Display(Name = "Client ID")]
        public string ClientId { get; set; }

        [Display(Name = "Topic")]
        public string Topic { get; set; }

        [Display(Name = "Client Username")]
        public string ClientUsername { get; set; }

        [Display(Name = "Client Password")]
        public string ClientPassword { get; set; }

        [Display(Name = "Keep Alive (s)")]
        [Range(0, 65535, ErrorMessage = "Keep alive must be between 0 and 65535")]
        public int KeepAlive { get; set; }

        [Display(Name = "SSL")]
        public bool EnableSsl { get; set; }

        //  SSL CERTIFICATE FIELDS

        [Display(Name = "Client Certificate File")]
        public string ClientCertificateFile { get; set; }

        [Display(Name = "Client Key File")]
        public string ClientKeyFile { get; set; }

        [Display(Name = "CA File")]
        public string CaFile { get; set; }

        //  TEST RESULTS 

        public bool IsConnected { get; set; }
        public string TestResult { get; set; }
        public int? ResponseTimeMs { get; set; }
        public List<MQTTDiagnosticCheck> Diagnostics { get; set; } = new();
    }

    public class MQTTDiagnosticCheck
    {
        public string Name { get; set; }
        public bool IsPassed { get; set; }
        public string Message { get; set; }
        public string Suggestion { get; set; }
        public string Details { get; set; }
    }
}