using System.Security.Claims;
using ERP.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ERP.Api.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class TablePermissionAttribute : TypeFilterAttribute
{
    public TablePermissionAttribute(string tableName, TablePermissionKind permission)
        : base(typeof(TablePermissionFilter))
    {
        Arguments = [tableName, permission];
    }
}

public sealed class TablePermissionFilter(
    AdminRepository permissions,
    string tableName,
    TablePermissionKind permission) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new ChallengeResult();
            return;
        }

        if (user.IsInRole("Admin")) return;

        if (!int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            context.Result = new ForbidResult();
            return;
        }

        if (!await permissions.HasPermissionAsync(userId, tableName, permission, context.HttpContext.RequestAborted))
            context.Result = new ForbidResult();
    }
}
