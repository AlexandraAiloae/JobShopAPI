using System.Collections.Generic;

namespace JobShopAPI.Models
{
    public class ScheduleData
    {
        public List<ScheduledOperation> Operations { get; set; } = new List<ScheduledOperation>();
        public string TotalProcessingTime { get; set; }
    }
}
