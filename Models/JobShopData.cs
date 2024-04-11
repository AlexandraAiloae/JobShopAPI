namespace JobShopAPI.Models
{
    public class JobShopData
    {
        public List<Machine> Machines { get; set; } = new List<Machine>();
        public List<Part> Parts { get; set; } = new List<Part>();
    }
}
