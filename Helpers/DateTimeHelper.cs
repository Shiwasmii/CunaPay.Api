using System;
using System.Globalization;

namespace CunaPay.Api.Helpers;

/// <summary>
/// Helper para manejar fechas y horas en la zona horaria de Bolivia (GMT-4)
/// </summary>
public static class DateTimeHelper
{
    private static readonly TimeZoneInfo BoliviaTimeZone;
    
    static DateTimeHelper()
    {
        // Intentar obtener la zona horaria de Bolivia
        // "SA Western Standard Time" es el ID de Windows para Bolivia
        // "America/La_Paz" es el ID de IANA para Bolivia
        try
        {
            // Intentar con el ID de Windows primero
            if (TimeZoneInfo.TryFindSystemTimeZoneById("SA Western Standard Time", out var tz))
            {
                BoliviaTimeZone = tz;
            }
            // Intentar con el ID de IANA (funciona en Linux/Mac)
            else if (TimeZoneInfo.TryFindSystemTimeZoneById("America/La_Paz", out var tz2))
            {
                BoliviaTimeZone = tz2;
            }
            else
            {
                // Fallback: crear una zona horaria UTC-4 manualmente
                BoliviaTimeZone = TimeZoneInfo.CreateCustomTimeZone(
                    "Bolivia Time",
                    TimeSpan.FromHours(-4),
                    "Bolivia Time",
                    "Bolivia Time");
            }
        }
        catch
        {
            // Fallback: crear una zona horaria UTC-4 manualmente
            BoliviaTimeZone = TimeZoneInfo.CreateCustomTimeZone(
                "Bolivia Time",
                TimeSpan.FromHours(-4),
                "Bolivia Time",
                "Bolivia Time");
        }
    }

    /// <summary>
    /// Obtiene la fecha y hora actual en la zona horaria de Bolivia (GMT-4)
    /// </summary>
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BoliviaTimeZone);

    /// <summary>
    /// Obtiene la fecha y hora actual en UTC (para almacenamiento en base de datos)
    /// </summary>
    public static DateTime UtcNow => DateTime.UtcNow;

    /// <summary>
    /// Convierte una fecha UTC a la zona horaria de Bolivia
    /// </summary>
    public static DateTime ToBoliviaTime(DateTime utcDateTime)
    {
        if (utcDateTime.Kind == DateTimeKind.Unspecified)
        {
            // Asumir que es UTC si no está especificado
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc), BoliviaTimeZone);
        }
        
        if (utcDateTime.Kind == DateTimeKind.Utc)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, BoliviaTimeZone);
        }
        
        // Si ya es local, convertir a UTC primero y luego a Bolivia
        return TimeZoneInfo.ConvertTime(utcDateTime, BoliviaTimeZone);
    }

    /// <summary>
    /// Convierte una fecha de Bolivia a UTC
    /// </summary>
    public static DateTime ToUtc(DateTime boliviaDateTime)
    {
        if (boliviaDateTime.Kind == DateTimeKind.Utc)
        {
            return boliviaDateTime;
        }

        if (boliviaDateTime.Kind == DateTimeKind.Unspecified)
        {
            // Asumir que es hora de Bolivia
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(boliviaDateTime, DateTimeKind.Unspecified), BoliviaTimeZone);
        }

        return TimeZoneInfo.ConvertTimeToUtc(boliviaDateTime, BoliviaTimeZone);
    }

    /// <summary>
    /// Formatea una fecha para mostrar en la zona horaria de Bolivia
    /// </summary>
    public static string FormatForDisplay(DateTime dateTime)
    {
        var boliviaTime = ToBoliviaTime(dateTime);
        return boliviaTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }
}

