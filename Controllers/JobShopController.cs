using Microsoft.AspNetCore.Mvc;
using JobShopAPI.Models;
using JobShopAPI.Services; // Assuming your service is in this namespace

[ApiController]
[Route("api/[controller]")]
public class JobShopController : ControllerBase
{
    private readonly IJobShopService _jobShopService;

    public JobShopController(IJobShopService jobShopService)
    {
        _jobShopService = jobShopService;
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
            var jobShopData = await _jobShopService.ProcessUploadedFileAsync(file);
            return Ok(jobShopData); // You might want to return something else
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
}
