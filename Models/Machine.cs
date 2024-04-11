namespace JobShopAPI.Models
{
    public class Machine
    {
        public string Name { get; set; }
        public int Capacity { get; set; } = 1; // Default to one part at a time
        public int CooldownTime { get; set; } // In seconds
    }
}
