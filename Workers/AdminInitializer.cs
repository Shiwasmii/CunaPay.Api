using BCrypt.Net;
using MongoDB.Driver;
using CunaPay.Api.Data;
using CunaPay.Api.Models;
using CunaPay.Api.Services;

namespace CunaPay.Api.Workers;

/// <summary>
/// Servicio que se ejecuta al iniciar la aplicación para crear el admin por defecto
/// </summary>
public class AdminInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AdminInitializer> _logger;
    private const string DEFAULT_ADMIN_EMAIL = "admin@gmail.com";
    private const string DEFAULT_ADMIN_PASSWORD = "admin123";

    public AdminInitializer(IServiceProvider serviceProvider, ILogger<AdminInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("AdminInitializer: Starting admin initialization...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
            var tronService = scope.ServiceProvider.GetRequiredService<TronService>();
            var cryptoService = scope.ServiceProvider.GetRequiredService<CryptoService>();
            var adminWalletService = scope.ServiceProvider.GetRequiredService<AdminWalletService>();

            // Verificar si ya existe el admin
            var existingAdmin = await db.Users.Find(u => u.Email == DEFAULT_ADMIN_EMAIL).FirstOrDefaultAsync(cancellationToken);

            if (existingAdmin != null)
            {
                _logger.LogInformation("AdminInitializer: Admin {Email} already exists", DEFAULT_ADMIN_EMAIL);
                
                // Asegurar que el admin tenga wallet
                await adminWalletService.EnsureAdminWalletAsync(existingAdmin.Id);
                _logger.LogInformation("AdminInitializer: Admin wallet ensured");
                return;
            }

            // Crear el admin
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(DEFAULT_ADMIN_PASSWORD, 10);
            var admin = new User
            {
                Email = DEFAULT_ADMIN_EMAIL,
                FirstName = "Admin",
                LastName = "System",
                PasswordHash = passwordHash,
                Role = "Admin",
                CreatedAt = Helpers.DateTimeHelper.UtcNow,
                UpdatedAt = Helpers.DateTimeHelper.UtcNow
            };

            await db.Users.InsertOneAsync(admin, cancellationToken: cancellationToken);
            _logger.LogInformation("AdminInitializer: Admin {Email} created successfully", DEFAULT_ADMIN_EMAIL);

            // Crear wallet para el admin
            var wallet = await adminWalletService.EnsureAdminWalletAsync(admin.Id);
            _logger.LogInformation("AdminInitializer: Admin wallet created: {Address}", wallet.Address);

            _logger.LogInformation("AdminInitializer: Admin initialization completed successfully");
            _logger.LogInformation("AdminInitializer: You can now login with:");
            _logger.LogInformation("AdminInitializer:   Email: {Email}", DEFAULT_ADMIN_EMAIL);
            _logger.LogInformation("AdminInitializer:   Password: {Password}", DEFAULT_ADMIN_PASSWORD);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdminInitializer: Error initializing admin");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("AdminInitializer: Stopping...");
        return Task.CompletedTask;
    }
}

