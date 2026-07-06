namespace ProtocolTester.Models
{
    public class TestResponse
    {
        public bool IsSuccessful { get; set; }
        public string Message { get; set; }
        public int ResponseTimeMs { get; set; }
        public object TestValue { get; set; }
        public string ErrorDetails { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public List<DiagnosticCheck> Diagnostics { get; set; } = new();
    }

    public class DiagnosticCheck
    {
        public string Name { get; set; }
        public bool IsPassed { get; set; }
        public string Message { get; set; }
        public string Suggestion { get; set; }
        public string Details { get; set; }
    }

    public class DiscoveredDevice
    {
        public int SlaveId { get; set; }
        public bool IsResponding { get; set; }
        public int ResponseTimeMs { get; set; }
        public object TestValue { get; set; }
    }
}
