using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MuktoAin.Web.Controllers;

namespace MuktoAin.UnitTests.Controllers;

public class HomeControllerTests
{
    [Fact]
    public void Chat_RendersIndexWithExplicitNonAmbiguousGetRoute()
    {
        var controller = new HomeController(Mock.Of<ILogger<HomeController>>());
        Assert.Equal("Index", Assert.IsType<ViewResult>(controller.Chat()).ViewName);
        var method = typeof(HomeController).GetMethod(nameof(HomeController.Chat))!;
        var route = Assert.Single(method.GetCustomAttributes(typeof(HttpGetAttribute), false)
            .Cast<HttpGetAttribute>());
        Assert.Equal("/Chat", route.Template);
        Assert.IsType<ViewResult>(controller.Index());
    }

    [Fact]
    public void Lawyer_IsRedirectedFromChatHomeToQueue()
    {
        var controller = new HomeController(Mock.Of<ILogger<HomeController>>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.Role, "Lawyer") }, "test"))
                }
            }
        };

        foreach (var result in new[] { controller.Index(), controller.Chat() })
        {
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Lawyer", redirect.ControllerName);
            Assert.Equal("Queue", redirect.ActionName);
        }
    }
}
