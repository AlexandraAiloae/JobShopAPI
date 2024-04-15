using JobShopAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JobShopAPI.Services
{
    public interface ISimpleSchedulerService
    {
        ScheduleData ScheduleOperations(JobShopData jobShopData);
    }

    public class SimpleSchedulerService : ISimpleSchedulerService
    {
        public ScheduleData ScheduleOperations(JobShopData jobShopData)
        {
            var schedule = new ScheduleData();
            var machineAvailability = new Dictionary<string, int>();

            // Initialize machine availability
            foreach (var machine in jobShopData.Machines)
            {
                machineAvailability[machine.Name] = 0; // All machines are available at time 0
            }

            foreach (var part in jobShopData.Parts.OrderBy(p => p.Operations.Sum(o => o.Duration)))
            {
                // Process each quantity of the part
                for (int quantityIndex = 0; quantityIndex < part.Quantity; quantityIndex++)
                {
                    int partStartTime = machineAvailability.Values.Max(); // Start at the latest time any machine becomes available

                    foreach (var operation in part.Operations)
                    {
                        // The operation start time is the maximum of the part start time or when the specific machine is next available
                        int startTime = Math.Max(partStartTime, machineAvailability[operation.MachineName]);

                        int endTime = startTime + operation.Duration;

                        schedule.Operations.Add(new ScheduledOperation
                        {
                            PartName = part.Name,
                            MachineName = operation.MachineName,
                            StartTime = FormatTime(startTime),
                            EndTime = FormatTime(endTime)
                        });

                        // Update machine availability for the next operation
                        machineAvailability[operation.MachineName] = endTime + GetCooldownTime(operation.MachineName, jobShopData);

                        // Update partStartTime to ensure the next operation starts right after the current one ends
                        partStartTime = endTime;
                    }
                }
            }

            // Compute the total processing time as the maximum of all end times
            schedule.TotalProcessingTime = FormatTime(machineAvailability.Values.Max());
            return schedule;
        }

        private string FormatTime(int totalSeconds)
        {
            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            int seconds = totalSeconds % 60;
            return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
        }

        private int GetCooldownTime(string machineName, JobShopData jobShopData)
        {
            var machine = jobShopData.Machines.FirstOrDefault(m => m.Name == machineName);
            return machine?.CooldownTime ?? 0; // Return the cooldown time, or 0 if not found
        }
    }
}
