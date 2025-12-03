using MongoDB.Driver;
using CunaPay.Api.Data;
using CunaPay.Api.Models;

namespace CunaPay.Api.Services;

public class AdminUserService
{
    private readonly MongoDbContext _db;
    private readonly ILogger<AdminUserService> _logger;

    public AdminUserService(
        MongoDbContext db,
        ILogger<AdminUserService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene todos los usuarios con paginación y filtros
    /// </summary>
    public async Task<(List<AdminUserDto> Users, int Total)> GetAllUsersAsync(
        int page = 1,
        int pageSize = 20,
        string? search = null,
        string? role = null)
    {
        var filterBuilder = Builders<User>.Filter;
        var filter = filterBuilder.Empty;

        // Filtro de búsqueda (email)
        if (!string.IsNullOrWhiteSpace(search))
        {
            filter &= filterBuilder.Regex(
                u => u.Email,
                new MongoDB.Bson.BsonRegularExpression(search, "i")); // Case insensitive
        }

        // Filtro por rol
        if (!string.IsNullOrWhiteSpace(role))
        {
            filter &= filterBuilder.Eq(u => u.Role, role);
        }

        var total = await _db.Users.CountDocumentsAsync(filter);

        var skip = (page - 1) * pageSize;
        var users = await _db.Users
            .Find(filter)
            .SortByDescending(u => u.CreatedAt)
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync();

        var userDtos = users.Select(u => MapToAdminDto(u)).ToList();

        return (userDtos, (int)total);
    }

    /// <summary>
    /// Obtiene un usuario por ID con todos sus datos
    /// </summary>
    public async Task<AdminUserDetailDto?> GetUserByIdAsync(string userId, AdminWalletService? adminWalletService = null)
    {
        var user = await _db.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        if (user == null)
            return null;

        // Obtener wallet del usuario (o crear si es admin y no tiene)
        var wallet = await _db.Wallets.Find(w => w.UserId == userId).FirstOrDefaultAsync();
        
        // Si es admin y no tiene wallet, crearla automáticamente
        if (wallet == null && user.Role == "Admin" && adminWalletService != null)
        {
            wallet = await adminWalletService.EnsureAdminWalletAsync(userId);
        }

        // Obtener estadísticas del usuario
        var stats = await GetUserStatsAsync(userId);

        return new AdminUserDetailDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            Wallet = wallet != null ? new AdminWalletDto
            {
                Id = wallet.Id,
                Address = wallet.Address,
                CreatedAt = wallet.CreatedAt
            } : null,
            Stats = stats
        };
    }

    /// <summary>
    /// Busca usuarios por email (búsqueda exacta o parcial)
    /// </summary>
    public async Task<List<AdminUserDto>> SearchUsersAsync(string searchTerm, int limit = 50)
    {
        var filter = Builders<User>.Filter.Regex(
            u => u.Email,
            new MongoDB.Bson.BsonRegularExpression(searchTerm, "i")); // Case insensitive

        var users = await _db.Users
            .Find(filter)
            .SortByDescending(u => u.CreatedAt)
            .Limit(limit)
            .ToListAsync();

        return users.Select(u => MapToAdminDto(u)).ToList();
    }

    /// <summary>
    /// Obtiene estadísticas de un usuario
    /// </summary>
    private async Task<AdminUserStatsDto> GetUserStatsAsync(string userId)
    {
        var wallet = await _db.Wallets.Find(w => w.UserId == userId).FirstOrDefaultAsync();
        if (wallet == null)
        {
            return new AdminUserStatsDto();
        }

        // Contar transacciones
        var transactionsCount = await _db.Transactions
            .CountDocumentsAsync(t => t.WalletId == wallet.Id);

        // Contar stakes
        var stakesCount = await _db.Stakes
            .CountDocumentsAsync(s => s.WalletId == wallet.Id);

        // Contar compras
        var purchasesCount = await _db.Purchases
            .CountDocumentsAsync(p => p.UserId == userId);

        // Obtener compras aceptadas
        var acceptedPurchases = await _db.Purchases
            .Find(p => p.UserId == userId && p.Status == "accepted")
            .ToListAsync();

        var totalUsdtPurchased = acceptedPurchases.Sum(p => p.AmountUsdt);

        return new AdminUserStatsDto
        {
            TransactionsCount = (int)transactionsCount,
            StakesCount = (int)stakesCount,
            PurchasesCount = (int)purchasesCount,
            TotalUsdtPurchased = totalUsdtPurchased
        };
    }

    /// <summary>
    /// Actualiza el rol de un usuario (solo admin)
    /// </summary>
    public async Task<AdminUserDto?> UpdateUserRoleAsync(string userId, string newRole)
    {
        if (newRole != "User" && newRole != "Admin")
            throw new ArgumentException("Role must be 'User' or 'Admin'");

        var update = Builders<User>.Update
            .Set(u => u.Role, newRole)
            .Set(u => u.UpdatedAt, Helpers.DateTimeHelper.UtcNow);

        var result = await _db.Users.UpdateOneAsync(
            Builders<User>.Filter.Eq(u => u.Id, userId),
            update);

        if (result.MatchedCount == 0)
            return null;

        var user = await _db.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        return user != null ? MapToAdminDto(user) : null;
    }

    private AdminUserDto MapToAdminDto(User user)
    {
        return new AdminUserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}

public class AdminUserDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class AdminUserDetailDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public AdminWalletDto? Wallet { get; set; }
    public AdminUserStatsDto Stats { get; set; } = new();
}

public class AdminWalletDto
{
    public string Id { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AdminUserStatsDto
{
    public int TransactionsCount { get; set; }
    public int StakesCount { get; set; }
    public int PurchasesCount { get; set; }
    public decimal TotalUsdtPurchased { get; set; }
}

