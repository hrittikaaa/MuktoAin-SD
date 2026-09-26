using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MuktoAin.Web.Auth;

// Citizen-side pages (case filing, tracking, drafts, document preview) are
// closed to every Lawyer-role account. The role is granted at registration,
// before bar verification, so these pages must not rely on verification status.
// Lawyers are sent to their own dashboard (Queue bounces unverified lawyers to
// Status); case data reaches them only through the claimed review workspace.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RedirectLawyersAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.HttpContext.User.IsInRole("Lawyer"))
            context.Result = new RedirectToActionResult("Queue", "Lawyer", null);
    }
}
