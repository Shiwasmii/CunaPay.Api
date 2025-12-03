using Microsoft.AspNetCore.Mvc;
using CunaPay.Api.Services;

namespace CunaPay.Api.Controllers;

[ApiController]
[Route("api/p2p")]
public class BinanceController : ControllerBase
{
    private readonly BinanceService _binanceService;
    private readonly ILogger<BinanceController> _logger;

    public BinanceController(BinanceService binanceService, ILogger<BinanceController> logger)
    {
        _binanceService = binanceService;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene el precio promedio de compra de USDT en Binance P2P
    /// </summary>
    /// <param name="asset">Activo (por defecto: USDT)</param>
    /// <param name="fiat">Moneda fiat (por defecto: BOB)</param>
    /// <param name="rows">Número de resultados a promediar (por defecto: 10)</param>
    /// <returns>Precio promedio de compra</returns>
    [HttpGet("usdt/buy/avg")]
    public async Task<IActionResult> GetAverageBuyPrice(
        [FromQuery] string asset = "USDT",
        [FromQuery] string fiat = "BOB",
        [FromQuery] int rows = 10)
    {
        var (success, averagePrice, error) = await _binanceService.GetAverageBuyPriceAsync(asset, fiat, rows);

        if (!success)
        {
            return BadRequest(new { ok = false, error = error });
        }

        return Ok(new
        {
            ok = true,
            promedio_precio = averagePrice?.ToString("F2"),
            precio = averagePrice,
            asset = asset,
            fiat = fiat,
            rows = rows
        });
    }

    /// <summary>
    /// Obtiene el precio promedio de venta de USDT en Binance P2P
    /// </summary>
    /// <param name="asset">Activo (por defecto: USDT)</param>
    /// <param name="fiat">Moneda fiat (por defecto: BOB)</param>
    /// <param name="rows">Número de resultados a promediar (por defecto: 10)</param>
    /// <returns>Precio promedio de venta</returns>
    [HttpGet("usdt/sell/avg")]
    public async Task<IActionResult> GetAverageSellPrice(
        [FromQuery] string asset = "USDT",
        [FromQuery] string fiat = "BOB",
        [FromQuery] int rows = 10)
    {
        var (success, averagePrice, error) = await _binanceService.GetAverageSellPriceAsync(asset, fiat, rows);

        if (!success)
        {
            return BadRequest(new { ok = false, error = error });
        }

        return Ok(new
        {
            ok = true,
            promedio_precio = averagePrice?.ToString("F2"),
            precio = averagePrice,
            asset = asset,
            fiat = fiat,
            rows = rows
        });
    }
}

