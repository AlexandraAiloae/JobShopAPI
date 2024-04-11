namespace JobShopAPI.Models
{
    public class Part
    {
        public string Name { get; set; }
        public int Quantity { get; set; }
        public List<Operation> Operations { get; set; } = new List<Operation>();
    }
}
