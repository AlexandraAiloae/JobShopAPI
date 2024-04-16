using JobShopAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Google.OrTools.Sat;

namespace JobShopAPI.Services
{
    public interface ISimpleSchedulerService
    {
        ScheduleData ScheduleSimpleJobShop(JobShopData jobShopData);
    }

    public class SimpleSchedulerService : ISimpleSchedulerService
    {
        public ScheduleData ScheduleSimpleJobShop(JobShopData jobShopData)
        {
            var model = new CpModel();
            var jobs = jobShopData.Parts; // Using parts as jobs
            int horizon = jobs.Sum(job => job.Operations.Sum(op => op.Duration) * job.Quantity);
            var allTasks = new Dictionary<(int, int, int), (IntVar start, IntVar end, IntervalVar interval)>();
            var machineToIntervals = new Dictionary<int, List<IntervalVar>>();

            foreach (var job in jobs.Select((job, idx) => (job, idx)))
            {
                int jobIdx = job.idx;
                for (int instance = 0; instance < job.job.Quantity; instance++)
                {
                    IntervalVar lastInterval = null;
                    foreach (var task in job.job.Operations.Select((op, idx) => (op, idx)))
                    {
                        var machineName = task.op.MachineName;
                        var machineId = jobShopData.Machines.FindIndex(m => m.Name == machineName);
                        if (machineId == -1)
                        {
                            Console.WriteLine($"Error: Machine name '{machineName}' not found in the machine list.");
                        }

                        var duration = task.op.Duration + GetCooldownTime(machineName, jobShopData);
                        var suffix = $"_{jobIdx}_{instance}_{task.idx}";
                        var startVar = model.NewIntVar(0, horizon, $"start{suffix}");
                        var endVar = model.NewIntVar(0, horizon, $"end{suffix}");
                        var intervalVar = model.NewIntervalVar(startVar, duration, endVar, $"interval{suffix}");

                        allTasks.Add((jobIdx, instance, task.idx), (startVar, endVar, intervalVar));
                        if (!machineToIntervals.ContainsKey(machineId))
                        {
                            machineToIntervals[machineId] = new List<IntervalVar>();
                        }
                        machineToIntervals[machineId].Add(intervalVar);

                        if (lastInterval != null)
                        {
                            model.Add(startVar == lastInterval.EndExpr());
                        }
                        lastInterval = intervalVar;
                    }
                }
            }

            foreach (var kvp in machineToIntervals)
            {
                var intervals = kvp.Value;
                model.AddNoOverlap(intervals);
            }

            var jobEnds = new List<IntVar>();
            foreach (var job in jobs.Select((job, idx) => (job, idx)))
            {
                for (int instance = 0; instance < job.job.Quantity; instance++)
                {
                    jobEnds.Add(allTasks[(job.idx, instance, job.job.Operations.Count - 1)].end);
                }
            }

            var makespan = model.NewIntVar(0, horizon, "makespan");
            model.AddMaxEquality(makespan, jobEnds);
            model.Minimize(makespan);

            var solver = new CpSolver();
            var status = solver.Solve(model);
            var schedule = new ScheduleData();
            if (status == CpSolverStatus.Optimal || status == CpSolverStatus.Feasible)
            {
                schedule.TotalProcessingTime = FormatTime((int)solver.ObjectiveValue);
                foreach (var task in allTasks)
                {
                    var partName = jobs[task.Key.Item1].Name;
                    var machineIndex = int.Parse(task.Value.interval.Name().Split('_').Last());
                    var machineName = jobShopData.Machines[machineIndex].Name;
                    schedule.Operations.Add(new ScheduledOperation
                    {
                        PartName = partName,
                        MachineName = machineName,
                        StartTime = FormatTime((int)solver.Value(task.Value.start)),
                        EndTime = FormatTime((int)solver.Value(task.Value.end))
                    });
                }
            }
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
