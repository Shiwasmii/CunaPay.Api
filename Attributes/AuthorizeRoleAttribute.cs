using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;
using MongoDB.Driver;
using CunaPay.Api.Data;
using CunaPay.Api.Models;

namespace CunaPay.Api.Attributes;

/// <summary>
/// Atributo personalizado para autorizar por roles
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class AuthorizeRoleAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _allowedRoles;

    public AuthorizeRoleAttribute(params string[] allowedRoles)
    {
        _allowedRoles = allowedRoles;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        // Verificar si el usuario está autenticado
        if (context.HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedObjectResult(new { error = "Authentication required" });
            return;
        }

        // Obtener el rol del usuario desde el token
        var role = context.HttpContext.User.FindFirst("role")?.Value 
                   ?? context.HttpContext.User.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(role))
        {
            // Si no hay rol en el token, obtenerlo de la base de datos
            var userId = context.HttpContext.User.FindFirst("uid")?.Value 
                        ?? context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                var db = context.HttpContext.RequestServices.GetRequiredService<MongoDbContext>();
                var user = db.Users.Find(u => u.Id == userId).FirstOrDefault();
                
                if (user != null)
                {
                    role = user.Role;
                    // Agregar el rol al claim para futuras peticiones
                    var identity = context.HttpContext.User.Identity as ClaimsIdentity;
                    identity?.AddClaim(new Claim("role", user.Role));
                }
            }
        }

        // Verificar si el rol está permitido
        if (string.IsNullOrEmpty(role) || !_allowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
        {
            context.Result = new ForbidResult();
            return;
        }
    }
}

