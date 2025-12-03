using MongoDB.Driver;
using CunaPay.Api.Data;
using CunaPay.Api.Models;

namespace CunaPay.Api.Services;

public class NewsService
{
    private readonly MongoDbContext _db;
    private readonly ILogger<NewsService> _logger;

    public NewsService(MongoDbContext db, ILogger<NewsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<NewsDto>> GetAllNewsAsync(int? limit = null, string? category = null)
    {
        var filterBuilder = Builders<News>.Filter;
        var filter = FilterDefinition<News>.Empty;

        if (!string.IsNullOrEmpty(category))
        {
            filter = filterBuilder.Eq(n => n.Category, category);
        }

        var query = _db.News.Find(filter).SortByDescending(n => n.CreatedAt);

        if (limit.HasValue && limit.Value > 0)
        {
            query = (IOrderedFindFluent<News, News>)query.Limit(limit.Value);
        }

        var news = await query.ToListAsync();

        return news.Select(n => new NewsDto
        {
            Id = n.Id,
            Title = n.Title,
            Category = n.Category,
            Link = n.Link,
            Image = n.Image,
            EconomicImpact = n.EconomicImpact,
            CreatedAt = n.CreatedAt,
            UpdatedAt = n.UpdatedAt
        }).ToList();
    }

    public async Task<NewsDto?> GetNewsByIdAsync(string id)
    {
        var news = await _db.News.Find(n => n.Id == id).FirstOrDefaultAsync();
        
        if (news == null)
        {
            return null;
        }

        return new NewsDto
        {
            Id = news.Id,
            Title = news.Title,
            Category = news.Category,
            Link = news.Link,
            Image = news.Image,
            EconomicImpact = news.EconomicImpact,
            CreatedAt = news.CreatedAt,
            UpdatedAt = news.UpdatedAt
        };
    }

    public async Task<NewsDto> CreateNewsAsync(CreateNewsRequest request)
    {
        var news = new News
        {
            Title = request.Title,
            Category = request.Category,
            Link = request.Link,
            Image = request.Image ?? string.Empty,
            EconomicImpact = request.EconomicImpact,
            CreatedAt = Helpers.DateTimeHelper.UtcNow,
            UpdatedAt = Helpers.DateTimeHelper.UtcNow
        };

        await _db.News.InsertOneAsync(news);

        return new NewsDto
        {
            Id = news.Id,
            Title = news.Title,
            Category = news.Category,
            Link = news.Link,
            Image = news.Image,
            EconomicImpact = news.EconomicImpact,
            CreatedAt = news.CreatedAt,
            UpdatedAt = news.UpdatedAt
        };
    }

    public async Task<NewsDto?> UpdateNewsAsync(string id, UpdateNewsRequest request)
    {
        var news = await _db.News.Find(n => n.Id == id).FirstOrDefaultAsync();
        
        if (news == null)
        {
            return null;
        }

        var updateBuilder = Builders<News>.Update;
        var updates = new List<UpdateDefinition<News>>();

        if (!string.IsNullOrEmpty(request.Title))
        {
            updates.Add(updateBuilder.Set(n => n.Title, request.Title));
        }

        if (!string.IsNullOrEmpty(request.Category))
        {
            updates.Add(updateBuilder.Set(n => n.Category, request.Category));
        }

        if (!string.IsNullOrEmpty(request.Link))
        {
            updates.Add(updateBuilder.Set(n => n.Link, request.Link));
        }

        if (request.Image != null)
        {
            updates.Add(updateBuilder.Set(n => n.Image, request.Image));
        }

        if (!string.IsNullOrEmpty(request.EconomicImpact))
        {
            updates.Add(updateBuilder.Set(n => n.EconomicImpact, request.EconomicImpact));
        }

        updates.Add(updateBuilder.Set(n => n.UpdatedAt, Helpers.DateTimeHelper.UtcNow));

        if (updates.Count > 0)
        {
            var combinedUpdate = updateBuilder.Combine(updates);
            await _db.News.UpdateOneAsync(
                Builders<News>.Filter.Eq(n => n.Id, id),
                combinedUpdate
            );
        }

        return await GetNewsByIdAsync(id);
    }

    public async Task<bool> DeleteNewsAsync(string id)
    {
        var result = await _db.News.DeleteOneAsync(n => n.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task<List<string>> GetCategoriesAsync()
    {
        var categories = await _db.News
            .Distinct<string>("category", FilterDefinition<News>.Empty)
            .ToListAsync();
        
        return categories.OrderBy(c => c).ToList();
    }
}

public class NewsDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string EconomicImpact { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateNewsRequest
{
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string? Image { get; set; }
    public string EconomicImpact { get; set; } = string.Empty;
}

public class UpdateNewsRequest
{
    public string? Title { get; set; }
    public string? Category { get; set; }
    public string? Link { get; set; }
    public string? Image { get; set; }
    public string? EconomicImpact { get; set; }
}

