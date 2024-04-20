using Microsoft.AspNetCore.Mvc;
using JobShopAPI.Models;
using JobShopAPI.Services; 

[ApiController]
[Route("api/[controller]")]
public class FileController : ControllerBase
{
    private readonly IFileServiceInputOne _fileServiceInputOne;
    private readonly ISimpleSchedulerService _simpleSchedulerService; 
    private readonly IFileServiceInputTwo _fileServiceInputTwo;
    private readonly IFlexibleSchedulerService _flexibleSchedulerService;

    public FileController(IFileServiceInputOne fileServiceInputOne, ISimpleSchedulerService simpleSchedulerService, IFileServiceInputTwo fileServiceInputTwo, IFlexibleSchedulerService flexibleSchedulerService)
    {
        _fileServiceInputOne = fileServiceInputOne;
        _simpleSchedulerService = simpleSchedulerService;
        _fileServiceInputTwo = fileServiceInputTwo;
        _flexibleSchedulerService = flexibleSchedulerService;
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
            ScheduleData scheduleData = null;

            if (file.FileName == "Input_One.txt")
            {
                jobShopData = await _fileServiceInputOne.ProcessUploadedFileAsync(file);
                scheduleData = _simpleSchedulerService.ScheduleSimpleJobShop(jobShopData);
            }
            else if (file.FileName == "Input_Two.txt")
            {
                jobShopData = await _fileServiceInputTwo.ProcessUploadedFileAsync(file);
                scheduleData = _flexibleSchedulerService.ScheduleFlexibleJobShop(jobShopData);

            }
            else
            {
                return BadRequest("Unsupported file name.");
            }
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
