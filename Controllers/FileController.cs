using Microsoft.AspNetCore.Mvc;
using JobShopAPI.Models;
using JobShopAPI.Services; // Assuming your service is in this namespace

[ApiController]
[Route("api/[controller]")]
public class FileController : ControllerBase
{
    private readonly IFileServiceInputOne _fileServiceInputOne;
    private readonly ISimpleSchedulerService _simpleSchedulerService; 
    private readonly IFileServiceInputTwo _fileServiceInputTwo;

    public FileController(IFileServiceInputOne fileServiceInputOne, ISimpleSchedulerService simpleSchedulerService, IFileServiceInputTwo fileServiceInputTwo)
    {
        _fileServiceInputOne = fileServiceInputOne;
        _simpleSchedulerService = simpleSchedulerService;
        _fileServiceInputTwo = fileServiceInputTwo;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        try
        {
            JobShopData jobShopData = null;

            // Determine the file processing service based on the file name
            if (file.FileName == "Input_One.txt")
            {
                // Processing for Input_One.txt
                jobShopData = await _fileServiceInputOne.ProcessUploadedFileAsync(file);
            }
            else if (file.FileName == "Input_Two.txt")
            {
                // Processing for Input_Two.txt
                jobShopData = await _fileServiceInputTwo.ProcessUploadedFileAsync(file);
            }
            else
            {
                // Handle unsupported file names
                return BadRequest("Unsupported file name.");
            }

            // Scheduling operations based on parsed data
            var scheduleData = _simpleSchedulerService.ScheduleSimpleJobShop(jobShopData);

            // Creating a response model that includes both data and schedule
            var responseModel = new
            {
                JobShopData = jobShopData,
                ScheduleData = scheduleData
            };

            return Ok(responseModel);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
}
