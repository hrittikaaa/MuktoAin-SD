using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using MuktoAin.Web.Auth;
using MuktoAin.Web.Controllers;

namespace MuktoAin.UnitTests.Controllers;

// #1: the citizen case/document pages are closed to every Lawyer-role account
// (pending, rejected or verified) -- lawyers reach case data only through
// their claimed review workspace.
public class RedirectLawyersAttributeTests
{
    private static ActionExecutingContext ContextFor(params Claim[] claims)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, claims.Length > 0 ? "test" : null))
        };
        return new ActionExecutingContext(
            new ActionContext(http, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(), new Dictionary<string, object?>(), controller: new object());
    }

    [Fact]
    public void Lawyer_IsRedirectedToQueue()
    {
        var context = ContextFor(new Claim(ClaimTypes.NameIdentifier, "7"), new Claim(ClaimTypes.Role, "Lawyer"));

        new RedirectLawyersAttribute().OnActionExecuting(context);

        var redirect = Assert.IsType<RedirectToActionResult>(context.Result);
        Assert.Equal("Lawyer", redirect.ControllerName);
        Assert.Equal("Queue", redirect.ActionName);
    }

    [Fact]
    public void CitizenAndGuest_PassThrough()
    {
        var citizen = ContextFor(new Claim(ClaimTypes.NameIdentifier, "42"), new Claim(ClaimTypes.Role, "Citizen"));
        var guest = ContextFor();

        new RedirectLawyersAttribute().OnActionExecuting(citizen);
        new RedirectLawyersAttribute().OnActionExecuting(guest);

        Assert.Null(citizen.Result);
        Assert.Null(guest.Result);
    }

    [Theory]
    [InlineData(typeof(CaseController))]
    [InlineData(typeof(DocumentController))]
    public void CitizenCaseControllers_CarryTheLawyerRedirect(Type controller)
    {
        Assert.True(controller.IsDefined(typeof(RedirectLawyersAttribute), inherit: true));
    }
}
