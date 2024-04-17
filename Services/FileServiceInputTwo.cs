using JobShopAPI.Models;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Machine = JobShopAPI.Models.Machine;

namespace JobShopAPI.Services
{
    public interface IFileServiceInputTwo
    {
        Task<JobShopData> ProcessUploadedFileAsync(IFormFile file);
    }

    public class FileServiceInputTwo : IFileServiceInputTwo
    {
        public async Task<JobShopData> ProcessUploadedFileAsync(IFormFile file)
        {
            // Read the content of the uploaded file
            string fileContent;
            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                fileContent = Encoding.UTF8.GetString(stream.ToArray());
            }

            // Use the parsing logic to populate JobShopData
            var jobShopData = ParseJobShopData(fileContent);

            return jobShopData;
        }

        private JobShopData ParseJobShopData(string fileContent)
        {
            var jobShopData = new JobShopData();
            var lines = fileContent.Split('\n')
                                   .Select(line => line.Trim())
                                   .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                                   .ToArray();

            ExtractMachineNames(lines, jobShopData);
            FillMachineDetails(lines, jobShopData);
            ExtractPartListAndQuantities(lines, jobShopData);
            FillPartOperations(lines, jobShopData);

            return jobShopData;
        }

        private void ExtractMachineNames(string[] lines, JobShopData jobShopData)
        {
            var machineNamesSectionIndex = Array.FindIndex(lines, line => line.StartsWith("Available machines:"));
            var machineFeaturesSectionIndex = Array.FindIndex(lines, line => line.StartsWith("Machine features:"));
            for (int i = machineNamesSectionIndex; i < machineFeaturesSectionIndex; i++)
            {
                var parts = lines[i].Split('.', StringSplitOptions.TrimEntries);
                if (parts.Length > 1)
                {
                    jobShopData.Machines.Add(new Machine { Name = parts[1] });
                }
            }
        }

        private void FillMachineDetails(string[] lines, JobShopData jobShopData)
        {
            var machineFeaturesSectionIndex = Array.FindIndex(lines, line => line.StartsWith("Machine features:"));
            var partListSectionIndex = Array.FindIndex(lines, line => line.StartsWith("Part list:"));
            var currentMachineIndex = -1;
            for (int i = machineFeaturesSectionIndex; i < partListSectionIndex; i++)
            {
                bool isFeatureIndexLine = false;

                if (int.TryParse(lines[i].Split(':')[0], out var machineIndex))
                {
                    currentMachineIndex = machineIndex - 1;
                    isFeatureIndexLine = true;
                }

                if (isFeatureIndexLine || currentMachineIndex >= 0 && currentMachineIndex < jobShopData.Machines.Count)
                {
                    UpdateMachineDetails(lines[i], jobShopData.Machines[currentMachineIndex]);

                }
            }
        }


        private void UpdateMachineDetails(string line, Machine machine)
        {
            if (line.Contains("Capacity"))
            {
                if (line.Contains("one part at a time"))
                {
                    machine.Capacity = 1;
                }
                else if (line.Contains("two parts at a time"))
                {
                    machine.Capacity = 2;
                }
                else if (line.Contains("no limit"))
                {
                    machine.Capacity = int.MaxValue; // Set capacity to a large value representing infinity
                }
            }
            else if (line.Contains("Cooldown time"))
            {
                var cooldownTimeStr = line.Split(':')[1].Trim();
                machine.CooldownTime = cooldownTimeStr.Equals("none", StringComparison.OrdinalIgnoreCase) ? 0 : ParseCooldownTime(cooldownTimeStr);
            }
        }

        private int ParseCooldownTime(string cooldownTimeStr)
        {
            var cooldownParts = cooldownTimeStr.Split(' ');
            if (cooldownParts.Length > 0 && int.TryParse(cooldownParts[0], out var cooldownSeconds))
            {
                return cooldownSeconds;
            }
            return 0;
        }

        private void ExtractPartListAndQuantities(string[] lines, JobShopData jobShopData)
        {
            var partListSectionIndex = Array.FindIndex(lines, line => line.StartsWith("Part list:"));
            var partOperationsSectionIndex = Array.FindIndex(lines, line => line.StartsWith("Part operations:"));
            for (int i = partListSectionIndex; i < partOperationsSectionIndex; i++)
            {
                var parts = lines[i].Split('-', StringSplitOptions.TrimEntries);
                if (parts.Length == 2 && int.TryParse(parts[1].Split(' ')[0], out var quantity))
                {
                    var partInfo = parts[0].Split('.', StringSplitOptions.TrimEntries);
                    if (partInfo.Length > 1)
                    {
                        jobShopData.Parts.Add(new Part { Name = partInfo[1].Trim(), Quantity = quantity });
                    }
                }
            }
        }

        private void FillPartOperations(string[] lines, JobShopData jobShopData)
        {
            var partOperationsSectionIndex = Array.FindIndex(lines, line => line.StartsWith("Part operations:"));
            var currentPartIndex = -1;

            // Available machine names
            var availableMachineNames = jobShopData.Machines.Select(m => m.Name).ToList();

            for (int i = partOperationsSectionIndex; i < lines.Length; i++)
            {
                bool isPartIndexLine = false;
                if (lines[i].Contains(':') && int.TryParse(lines[i].Split(':')[0], out var partIndex))
                {
                    currentPartIndex = partIndex - 1;
                    isPartIndexLine = true;
                }

                if ((isPartIndexLine || lines[i].Contains("seconds")) && currentPartIndex >= 0 && currentPartIndex < jobShopData.Parts.Count)
                {
                    var operationInfo = isPartIndexLine ? lines[i].Substring(lines[i].IndexOf(':') + 1).Trim() : lines[i];
                    AddOperationToPart(operationInfo, jobShopData.Parts[currentPartIndex], availableMachineNames);
                }
            }
        }

        private void AddOperationToPart(string operationInfo, Part part, List<string> availableMachineNames)
        {
            var operationParts = operationInfo.Split(':', StringSplitOptions.TrimEntries);
            if (operationParts.Length == 2)
            {
                var machineName = operationParts[0].Trim().TrimStart('-').Trim();

                // Find the closest match among available machine names
                var closestMatch = FindClosestMatch(machineName, availableMachineNames);

                var durationParts = operationParts[1].Trim().Split(' ');
                if (durationParts.Length > 1 && int.TryParse(durationParts[0], out var duration))
                {
                    part.Operations.Add(new Operation
                    {
                        MachineName = closestMatch,
                        Duration = duration
                    });
                }
            }
        }

        private string FindClosestMatch(string machineName, List<string> availableMachineNames)
        {
            var closestMatch = availableMachineNames
                .OrderBy(n => ComputeLevenshteinDistance(n, machineName))
                .FirstOrDefault();

            return closestMatch;
        }

        private int ComputeLevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.IsNullOrEmpty(t) ? 0 : t.Length;
            }

            if (string.IsNullOrEmpty(t))
            {
                return s.Length;
            }

            int[,] distance = new int[s.Length + 1, t.Length + 1];

            for (int i = 0; i <= s.Length; distance[i, 0] = i++) ;
            for (int j = 0; j <= t.Length; distance[0, j] = j++) ;

            for (int i = 1; i <= s.Length; i++)
            {
                for (int j = 1; j <= t.Length; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    distance[i, j] = Math.Min(
                        Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                        distance[i - 1, j - 1] + cost);
                }
            }

            return distance[s.Length, t.Length];
        }

    }
}
