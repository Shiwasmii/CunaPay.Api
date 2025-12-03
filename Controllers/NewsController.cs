using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CunaPay.Api.Services;
using CunaPay.Api.Attributes;

namespace CunaPay.Api.Controllers;

[ApiController]
[Route("api/news")]
[Authorize]
public class NewsController : ControllerBase
{
    private readonly NewsService _newsService;
    private readonly ILogger<NewsController> _logger;

    public NewsController(NewsService newsService, ILogger<NewsController> logger)
    {
        _newsService = newsService;
        _logger = logger;
    }

    /// <summary>
    /// Obtener todas las noticias
    /// </summary>
    /// <param name="limit">Límite de resultados (opcional)</param>
    /// <param name="category">Filtrar por categoría (opcional)</param>
    /// <returns>Lista de noticias</returns>
    [HttpGet]
    public async Task<IActionResult> GetAllNews([FromQuery] int? limit = null, [FromQuery] string? category = null)
    {
        try
        {
            var news = await _newsService.GetAllNewsAsync(limit, category);
            return Ok(new { items = news, total = news.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting news");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Crear nueva noticia (Solo Admin)
    /// </summary>
    /// <param name="request">Datos de la noticia</param>
    /// <returns>Noticia creada</returns>
    [HttpPost]
    [AuthorizeRole("Admin")]
    public async Task<IActionResult> CreateNews([FromBody] CreateNewsRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest(new { error = "Title is required" });
            }

            if (string.IsNullOrWhiteSpace(request.Category))
            {
                return BadRequest(new { error = "Category is required" });
            }

            if (string.IsNullOrWhiteSpace(request.Link))
            {
                return BadRequest(new { error = "Link is required" });
            }

            var news = await _newsService.CreateNewsAsync(request);
            return StatusCode(201, news);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating news");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Actualizar noticia (Solo Admin)
    /// </summary>
    /// <param name="id">ID de la noticia</param>
    /// <param name="request">Datos a actualizar</param>
    /// <returns>Noticia actualizada</returns>
    [HttpPut("{id}")]
    [AuthorizeRole("Admin")]
    public async Task<IActionResult> UpdateNews(string id, [FromBody] UpdateNewsRequest request)
    {
        try
        {
            var news = await _newsService.UpdateNewsAsync(id, request);
            
            if (news == null)
            {
                return NotFound(new { error = "News not found" });
            }

            return Ok(news);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating news");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Eliminar noticia (Solo Admin)
    /// </summary>
    /// <param name="id">ID de la noticia</param>
    /// <returns>Resultado de la eliminación</returns>
    [HttpDelete("{id}")]
    [AuthorizeRole("Admin")]
    public async Task<IActionResult> DeleteNews(string id)
    {
        try
        {
            var deleted = await _newsService.DeleteNewsAsync(id);
            
            if (!deleted)
            {
                return NotFound(new { error = "News not found" });
            }

            return Ok(new { ok = true, message = "News deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting news");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Obtener todas las categorías disponibles
    /// </summary>
    /// <returns>Lista de categorías</returns>
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        try
        {
            var categories = await _newsService.GetCategoriesAsync();
            return Ok(new { categories });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting categories");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}

