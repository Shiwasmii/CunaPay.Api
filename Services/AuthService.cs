using BCrypt.Net;
using MongoDB.Driver;
using CunaPay.Api.Data;
using CunaPay.Api.Models;
using CunaPay.Api.Services;

namespace CunaPay.Api.Services;

public class AuthService
{
    private readonly MongoDbContext _db;
    private readonly JwtService _jwtService;
    private readonly TronService _tronService;
    private readonly CryptoService _cryptoService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        MongoDbContext db,
        JwtService jwtService,
        TronService tronService,
        CryptoService cryptoService,
        ILogger<AuthService> logger)
    {
        _db = db;
        _jwtService = jwtService;
        _tronService = tronService;
        _cryptoService = cryptoService;
        _logger = logger;
    }

    public async Task<(string Token, UserDto User, WalletDto? Wallet)> RegisterAsync(string email, string password, string firstName, string lastName)
    {
        var existing = await _db.Users.Find(u => u.Email == email).FirstOrDefaultAsync();
        if (existing != null)
        {
            throw new InvalidOperationException("Email already registered");
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, 10);
        var user = new User
        {
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            PasswordHash = passwordHash,
            Role = "User", // Solo usuarios normales pueden registrarse
            CreatedAt = Helpers.DateTimeHelper.UtcNow,
            UpdatedAt = Helpers.DateTimeHelper.UtcNow
        };

        await _db.Users.InsertOneAsync(user);

        // Create wallet
        var (address, privateKey) = await _tronService.CreateWalletAsync();
        var pkEncrypted = _cryptoService.Encrypt(privateKey);

        var wallet = new Wallet
        {
            UserId = user.Id,
            Address = address,
            PkEncrypted = pkEncrypted,
            CreatedAt = Helpers.DateTimeHelper.UtcNow,
            UpdatedAt = Helpers.DateTimeHelper.UtcNow
        };

        await _db.Wallets.InsertOneAsync(wallet);

        var token = _jwtService.GenerateToken(user.Id, user.Email, user.Role);

        return (token, new UserDto { Id = user.Id, Email = user.Email, FirstName = user.FirstName, LastName = user.LastName, Role = user.Role, BankAccountNumber = user.BankAccountNumber, BankEntity = user.BankEntity }, 
            new WalletDto { Id = wallet.Id, Address = wallet.Address });
    }

    public async Task<(string Token, UserDto User)> LoginAsync(string email, string password)
    {
        var user = await _db.Users.Find(u => u.Email == email).FirstOrDefaultAsync();
        if (user == null)
        {
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        var isValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        if (!isValid)
        {
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        var token = _jwtService.GenerateToken(user.Id, user.Email, user.Role);
        return (token, new UserDto { Id = user.Id, Email = user.Email, FirstName = user.FirstName, LastName = user.LastName, Role = user.Role, BankAccountNumber = user.BankAccountNumber, BankEntity = user.BankEntity });
    }

    public async Task ChangePasswordAsync(string userId, string currentPassword, string newPassword)
    {
        var user = await _db.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        // Verificar que la contraseña actual sea correcta
        var isValid = BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash);
        if (!isValid)
        {
            throw new UnauthorizedAccessException("Current password is incorrect");
        }

        // Verificar que la nueva contraseña sea diferente a la actual
        var isSamePassword = BCrypt.Net.BCrypt.Verify(newPassword, user.PasswordHash);
        if (isSamePassword)
        {
            throw new InvalidOperationException("New password must be different from current password");
        }

        // Validar que la nueva contraseña tenga al menos 6 caracteres
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            throw new ArgumentException("New password must be at least 6 characters long");
        }

        // Hashear la nueva contraseña
        var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, 10);

        // Actualizar la contraseña
        var update = Builders<User>.Update
            .Set(u => u.PasswordHash, newPasswordHash)
            .Set(u => u.UpdatedAt, Helpers.DateTimeHelper.UtcNow);

        await _db.Users.UpdateOneAsync(
            u => u.Id == userId,
            update);

        _logger.LogInformation("Password changed successfully for user {UserId}", userId);
    }
}

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public string? BankAccountNumber { get; set; }
    public string? BankEntity { get; set; }
}

public class WalletDto
{
    public string Id { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}

