namespace JobShopAPI.Models
{
    public class ScheduledOperation
    {
        public string PartName { get; set; }
        public string MachineName { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
    }
}
