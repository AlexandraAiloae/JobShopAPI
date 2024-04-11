namespace JobShopAPI.Models
{
    public class Machine
    {
        public string Name { get; set; }
        public int Capacity { get; set; }
        public int CooldownTime { get; set; } // In seconds
    }
}
