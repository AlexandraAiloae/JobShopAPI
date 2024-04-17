using Microsoft.AspNetCore.Mvc;
using JobShopAPI.Models;
using JobShopAPI.Services; // Assuming your service is in this namespace

[ApiController]
[Route("api/[controller]")]
public class FileController : ControllerBase
{
    private readonly IFileService _fileService;
    private readonly ISimpleSchedulerService _simpleSchedulerService; 

    public FileController(IFileService fileService, ISimpleSchedulerService simpleSchedulerService)
    {
        _fileService = fileService;
        _simpleSchedulerService = simpleSchedulerService;
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
            // Parsing the uploaded file
            var jobShopData = await _fileService.ProcessUploadedFileAsync(file);

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
