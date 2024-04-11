using JobShopAPI.Models;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using Machine = JobShopAPI.Models.Machine;

namespace JobShopAPI.Services
{
    public interface IJobShopService
    {
        Task<JobShopData> ProcessUploadedFileAsync(IFormFile file);
    }

    public class JobShopService : IJobShopService
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
            // Split the file content into lines, then immediately filter out comments and empty lines
            var lines = fileContent.Split('\n')
                                   .Select(line => line.Trim()) // Trim each line to ensure empty lines are correctly identified
                                   .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                                   .ToArray();

            // Now, lines array doesn't contain any comments or purely empty lines
            var machineNamesSectionIndex = Array.FindIndex(lines, line => line.StartsWith("Available machines:")) + 1;
            var machineFeaturesSectionIndex = Array.FindIndex(lines, line => line.StartsWith("Machine features:"));
            var partListSectionIndex = Array.FindIndex(lines, line => line.StartsWith("Part list:")) + 1;
            var partOperationsSectionIndex = Array.FindIndex(lines, line => line.StartsWith("Part operations:"));

            // Extract machine names
            for (int i = machineNamesSectionIndex; i < machineFeaturesSectionIndex && i != -1; i++)
            {
                var parts = lines[i].Split('.', StringSplitOptions.TrimEntries);
                if (parts.Length > 1)
                {
                    jobShopData.Machines.Add(new Machine { Name = parts[1] });
                }
            }

            // Fill in machine details
            var currentMachineIndex = -1;
            for (int i = machineFeaturesSectionIndex + 1; i < lines.Length && i != -1; i++)
            {
                if (lines[i].Contains(':') && int.TryParse(lines[i].Split(':')[0], out var machineIndex))
                {
                    currentMachineIndex = machineIndex - 1;
                }
                else if (lines[i].Contains("Capacity") && lines[i].Contains("one part at a time"))
                {
                    if (currentMachineIndex >= 0 && currentMachineIndex < jobShopData.Machines.Count)
                    {
                        jobShopData.Machines[currentMachineIndex].Capacity = 1;
                    }
                }
                else if (lines[i].Contains("Cooldown time"))
                {
                    if (currentMachineIndex >= 0 && currentMachineIndex < jobShopData.Machines.Count)
                    {
                        var cooldownTimeStr = lines[i].Split(':')[1].Trim();
                        if (cooldownTimeStr.Equals("none", StringComparison.OrdinalIgnoreCase))
                        {
                            jobShopData.Machines[currentMachineIndex].CooldownTime = 0;
                        }
                        else
                        {
                            var cooldownParts = cooldownTimeStr.Split(' ');
                            if (cooldownParts.Length > 0 && int.TryParse(cooldownParts[0], out var cooldownSeconds))
                            {
                                jobShopData.Machines[currentMachineIndex].CooldownTime = cooldownSeconds;
                            }
                        }
                    }
                }
            }

            // Extract part list and quantities
            for (int i = partListSectionIndex; i < partOperationsSectionIndex && i != -1; i++)
            {
                var parts = lines[i].Split('-', StringSplitOptions.TrimEntries);
                if (parts.Length == 2)
                {
                    var partInfo = parts[0].Split('.', StringSplitOptions.TrimEntries);
                    if (partInfo.Length > 1 && int.TryParse(parts[1].Split(' ')[0], out var quantity))
                    {
                        jobShopData.Parts.Add(new Part { Name = partInfo[1].Trim(), Quantity = quantity });
                    }
                }
            }

            // Fill in part operations
            var currentPartIndex = -1;
            for (int i = partOperationsSectionIndex + 1; i < lines.Length && i != -1; i++)
            {
                bool isPartIndexLine = false;
                if (lines[i].Contains(':') && int.TryParse(lines[i].Split(':')[0], out var partIndex))
                {
                    currentPartIndex = partIndex - 1;
                    isPartIndexLine = true;
                }

                // Check if the line contains an operation. This check is done in both conditions: 
                // when the line is a part index (which might also include an operation), and 
                // for all subsequent lines that don't change the currentPartIndex.
                if ((isPartIndexLine || lines[i].Contains("seconds")) && currentPartIndex >= 0 && currentPartIndex < jobShopData.Parts.Count)
                {
                    // Try to parse the operation from the current line.
                    // When isPartIndexLine is true, the operation might be on the same line as the part index.
                    var operationInfo = isPartIndexLine ? lines[i].Substring(lines[i].IndexOf(':') + 1).Trim() : lines[i];
                    var operationParts = operationInfo.Split(':', StringSplitOptions.TrimEntries);
                    if (operationParts.Length == 2)
                    {
                        var durationParts = operationParts[1].Trim().Split(' ');
                        if (durationParts.Length > 1 && int.TryParse(durationParts[0], out var duration))
                        {
                            jobShopData.Parts[currentPartIndex].Operations.Add(new Operation
                            {
                                MachineName = operationParts[0].Trim(),
                                Duration = duration
                            });
                        }
                    }
                }
            }


            return jobShopData;
        }

    }
}
