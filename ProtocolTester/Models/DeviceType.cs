namespace ProtocolTester.Models
{
    public class DeviceType
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public int MaxRegistersPerRead { get; set; }
        public List<Mapping> Mappings { get; set; } = new();
    }

    public class Mapping
    {
        public string Name { get; set; }
        public string RegisterType { get; set; }
        public int StartAddress { get; set; }
        public int Count { get; set; }
        public string DataType { get; set; }
        public int Id { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public int Factor { get; set; }
        public string ByteOrder { get; set; }
        public string Unit { get; set; }
        public string AccessRight { get; set; }
    }
}