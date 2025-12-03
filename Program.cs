using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using CunaPay.Api.Data;
using CunaPay.Api.Services;
using CunaPay.Api.Workers;
using CunaPay.Api.Configuration;
using CunaPay.Api.Middlewares;
using TronNet;
using CunaPay.Api.Patterns.Structural;
using CunaPay.Api.Patterns.Behavioral;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// MongoDB
builder.Services.AddSingleton<MongoDbContext>();

// Services
builder.Services.AddSingleton<CryptoService>();
builder.Services.AddSingleton<JwtService>();
builder.Services.AddHttpClient("default")
    .ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.HttpClientHandler()
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All
    });

// TronNet Configuration
var tronSettings = builder.Configuration.GetSection("Tron").Get<TronSettings>() ?? new TronSettings();
try
{
    builder.Services.AddTronNet(options =>
    {
        var isTestNet = tronSettings.FullNode.Contains("nile", StringComparison.OrdinalIgnoreCase) ||
                       tronSettings.FullNode.Contains("shasta", StringComparison.OrdinalIgnoreCase);
        
        options.Network = isTestNet ? TronNetwork.TestNet : TronNetwork.MainNet;
    });
}
catch (Exception ex)
{
    Console.WriteLine($"Warning: Could not configure TronNet automatically: {ex.Message}");
    Console.WriteLine("TronService will use HTTP API directly.");
}

// Core Services
builder.Services.AddScoped<TronService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<StakingService>();
builder.Services.AddScoped<NewsService>();
builder.Services.AddScoped<BinanceService>();
builder.Services.AddScoped<PurchaseService>();
builder.Services.AddScoped<WithdrawalService>();
builder.Services.AddScoped<AdminUserService>();
builder.Services.AddScoped<AdminWalletService>();

// Rate limiting and idempotency
builder.Services.AddSingleton<IRateLimitService, RateLimitService>();
builder.Services.AddSingleton<IIdempotencyService, InMemoryIdempotencyService>();

// Memory Cache (para Decorator Pattern)
builder.Services.AddMemoryCache();

// WalletService con Decorator Pattern (Cache + Logging)
builder.Services.AddScoped<WalletService>();
builder.Services.AddScoped<IWalletService>(sp =>
{
    var walletService = sp.GetRequiredService<WalletService>();
    var loggedService = new LoggedWalletService(
        walletService,
        sp.GetRequiredService<ILogger<LoggedWalletService>>());
    var cachedService = new CachedWalletService(
        loggedService,
        sp.GetRequiredService<IMemoryCache>(),
        sp.GetRequiredService<ILogger<CachedWalletService>>());
    return cachedService;
});

// Event Bus para notificaciones de transacciones
builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();
builder.Services.AddScoped<TransactionNotificationHandler>();

// Background Services
builder.Services.AddHostedService<TransactionWatcher>();
builder.Services.AddHostedService<AdminInitializer>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "dev_jwt_secret_key_min_32_chars";
var key = Encoding.UTF8.GetBytes(jwtSecret);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Suscribir handlers a eventos de transacciones
using (var scope = app.Services.CreateScope())
{
    var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
    var notificationHandler = scope.ServiceProvider.GetRequiredService<TransactionNotificationHandler>();
    
    eventBus.Subscribe<TransactionBroadcastedEvent>(notificationHandler);
    eventBus.Subscribe<TransactionConfirmedEvent>(notificationHandler);
    eventBus.Subscribe<TransactionFailedEvent>(notificationHandler);
    
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Archivos estáticos
app.UseStaticFiles();

app.MapControllers();

// Health check endpoint
app.MapGet("/", () => new
{
    ok = true,
    service = "cunapay",
    env = app.Environment.EnvironmentName
});

// Obtener el puerto de la variable de entorno PORT (Render lo proporciona automáticamente)
// Si no existe, usar el puerto de configuración o 4000 por defecto
var port = Environment.GetEnvironmentVariable("PORT");
if (string.IsNullOrEmpty(port))
{
    port = builder.Configuration.GetValue<string>("Port", "4000");
}
app.Run($"http://0.0.0.0:{port}");

