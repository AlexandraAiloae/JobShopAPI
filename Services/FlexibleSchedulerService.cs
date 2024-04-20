using JobShopAPI.Models;
using Google.OrTools.Sat;

namespace JobShopAPI.Services
{
    public interface IFlexibleSchedulerService
    {
        ScheduleData ScheduleFlexibleJobShop(JobShopData jobShopData);
    }

    public class FlexibleSchedulerService : IFlexibleSchedulerService
    {
        public ScheduleData ScheduleFlexibleJobShop(JobShopData jobShopData)
        {
            var model = new CpModel();
            var jobs = jobShopData.Parts;
            int horizon = CalculateHorizon(jobs, jobShopData);
            var allTasks = new Dictionary<(int, int, int), (IntVar start, IntVar end, IntervalVar interval)>();
            var machineToIntervals = new Dictionary<int, List<IntervalVar>>();

            CreateTasksAndIntervals(model, jobs, horizon, allTasks, machineToIntervals, jobShopData);

            AddNoOverlapConstraint(model, machineToIntervals);

            var makespan = AddMakespanObjective(model, jobs, allTasks, horizon);

            var solver = new CpSolver();
            var status = solver.Solve(model);

            var schedule = new ScheduleData();
            if (status == CpSolverStatus.Optimal || status == CpSolverStatus.Feasible)
            {
                PopulateScheduleData(schedule, jobs, allTasks, solver, jobShopData);
            }
            return schedule;
        }


        private int CalculateHorizon(IEnumerable<Part> jobs, JobShopData jobShopData)
        {
            return jobs.Sum(job => job.Operations.Sum(op => op.Duration) * job.Quantity) +
                   jobs.Sum(job => job.Operations.Sum(op => GetCooldownTime(op.MachineName, jobShopData)));
        }

        private void CreateTasksAndIntervals(CpModel model, IEnumerable<Part> jobs, int horizon,
                                     Dictionary<(int, int, int), (IntVar start, IntVar end, IntervalVar interval)> allTasks,
                                     Dictionary<int, List<IntervalVar>> machineToIntervals, JobShopData jobShopData)
        {
            foreach (var (job, jobIdx) in jobs.Select((job, idx) => (job, idx)))
            {
                for (int instance = 0; instance < job.Quantity; instance++)
                {
                    IntervalVar lastInterval = null;
                    IntVar lastEndVar = null; 

                    foreach (var (op, opIdx) in job.Operations.Select((op, idx) => (op, idx)))
                    {
                        var machineName = op.MachineName;
                        var machineId = jobShopData.Machines.FindIndex(m => m.Name == machineName);
                        if (machineId == -1)
                        {
                            Console.WriteLine($"Error: Machine name '{machineName}' not found in the machine list.");
                        }

                        var duration = op.Duration + GetCooldownTime(machineName, jobShopData);
                        var suffix = $"_{jobIdx}_{instance}_{opIdx}";
                        var startVar = model.NewIntVar(0, horizon, $"start{suffix}");
                        var endVar = model.NewIntVar(0, horizon, $"end{suffix}");
                        var intervalVar = model.NewIntervalVar(startVar, duration, endVar, $"interval{suffix}");

                        allTasks.Add((jobIdx, instance, opIdx), (startVar, endVar, intervalVar));
                        if (!machineToIntervals.ContainsKey(machineId))
                        {
                            machineToIntervals[machineId] = new List<IntervalVar>();
                        }
                        machineToIntervals[machineId].Add(intervalVar);

                        if (lastInterval != null)
                        {
                            model.Add(startVar >= lastEndVar);
                        }

                        lastInterval = intervalVar;
                        lastEndVar = endVar; 
                    }
                }
            }
        }


        private void AddNoOverlapConstraint(CpModel model, Dictionary<int, List<IntervalVar>> machineToIntervals)
        {
            foreach (var intervals in machineToIntervals.Values)
            {
                model.AddNoOverlap(intervals);
            }
        }

        private IntVar AddMakespanObjective(CpModel model, IEnumerable<Part> jobs,
                                             Dictionary<(int, int, int), (IntVar start, IntVar end, IntervalVar interval)> allTasks,
                                             int horizon)
        {
            var jobEnds = new List<IntVar>();
            foreach (var (job, jobIdx) in jobs.Select((job, idx) => (job, idx)))
            {
                for (int instance = 0; instance < job.Quantity; instance++)
                {
                    jobEnds.Add(allTasks[(jobIdx, instance, job.Operations.Count - 1)].end);
                }
            }

            var makespan = model.NewIntVar(0, horizon, "makespan");
            model.AddMaxEquality(makespan, jobEnds);
            model.Minimize(makespan);
            return makespan;
        }

        private void PopulateScheduleData(ScheduleData schedule, IEnumerable<Part> jobs,
                                 Dictionary<(int, int, int), (IntVar start, IntVar end, IntervalVar interval)> allTasks,
                                 CpSolver solver, JobShopData jobShopData)
        {
            schedule.TotalProcessingTime = FormatTime((int)solver.ObjectiveValue);
            foreach (var task in allTasks)
            {
                var (jobIdx, instance, taskIdx) = task.Key;
                var partName = jobs.ElementAt(jobIdx).Name;
                var machineName = jobs.ElementAt(jobIdx).Operations.ElementAt(taskIdx).MachineName; 
                var startTime = (int)solver.Value(task.Value.start);
                var endTime = (int)solver.Value(task.Value.end);
                schedule.Operations.Add(new ScheduledOperation
                {
                    PartName = partName,
                    MachineName = machineName,
                    StartTime = FormatTime(startTime),
                    EndTime = FormatTime(endTime)
                });
            }
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
            return machine?.CooldownTime ?? 0; 
        }
    }
}



