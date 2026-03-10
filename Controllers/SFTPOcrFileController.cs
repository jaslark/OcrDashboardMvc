using Microsoft.AspNetCore.Mvc;
using OcrDashboardMvc.Models;
using OcrDashboardMvc.Repositories;

namespace OcrDashboardMvc.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SFTPOcrFileController : ControllerBase
    {
        private readonly ISFTPOcrFileRepository _repository;
        private readonly ILogger<SFTPOcrFileController> _logger;

        public SFTPOcrFileController(
            ISFTPOcrFileRepository repository,
            ILogger<SFTPOcrFileController> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        // GET: api/SFTPOcrFile
        [HttpGet]
        public async Task<ActionResult<List<SFTPOcrFile>>> GetAll()
        {
            try
            {
                var files = await _repository.GetAllAsync();
                return Ok(files);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all SFTP OCR files");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/SFTPOcrFile/5
        [HttpGet("{id}")]
        public async Task<ActionResult<SFTPOcrFile>> GetById(long id)
        {
            try
            {
                var file = await _repository.GetByIdAsync(id);
                if (file == null)
                {
                    return NotFound();
                }
                return Ok(file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting SFTP OCR file by id: {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/SFTPOcrFile/paged?page=1&itemsPerPage=10
        [HttpGet("paged")]
        public async Task<ActionResult> GetPaged(long page = 1, long itemsPerPage = 10)
        {
            try
            {
                var pagedResult = await _repository.GetPagedAsync(page, itemsPerPage);
                return Ok(new
                {
                    items = pagedResult.Items,
                    currentPage = pagedResult.CurrentPage,
                    totalPages = pagedResult.TotalPages,
                    totalItems = pagedResult.TotalItems,
                    itemsPerPage = pagedResult.ItemsPerPage
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paged SFTP OCR files");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/SFTPOcrFile/status/1
        [HttpGet("status/{status}")]
        public async Task<ActionResult<List<SFTPOcrFile>>> GetByStatus(int status)
        {
            try
            {
                var files = await _repository.GetByStatusAsync(status);
                return Ok(files);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting SFTP OCR files by status: {Status}", status);
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/SFTPOcrFile
        [HttpPost]
        public async Task<ActionResult<SFTPOcrFile>> Create(SFTPOcrFile file)
        {
            try
            {
                file.Created = DateTime.Now;
                var id = await _repository.InsertAsync(file);
                file.ID = id;
                return CreatedAtAction(nameof(GetById), new { id }, file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating SFTP OCR file");
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/SFTPOcrFile/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(long id, SFTPOcrFile file)
        {
            try
            {
                if (id != file.ID)
                {
                    return BadRequest("ID mismatch");
                }

                file.Updated = DateTime.Now;
                var result = await _repository.UpdateAsync(file);

                if (result == 0)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating SFTP OCR file: {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // DELETE: api/SFTPOcrFile/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            try
            {
                var result = await _repository.DeleteAsync(id);

                if (result == 0)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting SFTP OCR file: {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
