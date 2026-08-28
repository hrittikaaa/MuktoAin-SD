# Frontend Conventions — Shared Across All Views

> Read this before building any `*-F.#` task in `Dependency_plan.md`. It applies no matter
> which of the three plans (`Shads_plan.md`, `Hrittika_plan.md`, `Arpita_plan.md`) a given view
> task lives under.

## Build views mock-first, then wire directly

Since every view is owned by the same person who owns its backend service, there's no handoff
between a "mock builder" and a "real-service wirer" — but it's still worth building the
`Controller` + `View` + `ViewModel` against mock data *first*, then swapping the mock return for
a real service call once that service exists. This keeps the two concerns separate and gives you
something clickable to demo before the backend is fully wired up.

```csharp
public class CaseController : Controller
{
    // private readonly CaseService _caseService;   // add once CaseService exists

    [HttpGet]
    public IActionResult Submit()
    {
        var vm = new CaseSubmitViewModel
        {
            Categories = MockData.Categories,   // hardcoded list, swap for a real query later
            Districts = MockData.Districts,
        };
        return View(vm);
    }

    [HttpPost]
    public IActionResult Submit(CaseSubmitViewModel vm)
    {
        // Swap this for _caseService.SubmitCaseAsync(...) once CaseService is ready.
        TempData["Success"] = "Case submitted successfully! Case ID: MKT-2024-0042";
        return RedirectToAction("Track");
    }
}
```

Create a single `MockData.cs` helper in `MuktoAin.Web/` that holds all fake data in one place —
every view task below adds its own mock objects to it:

```csharp
public static class MockData
{
    public static List<SelectListItem> Categories => new()
    {
        new("Labour Complaint", "1"),
        new("General Diary (GD)", "2"),
        new("RTI Request", "3"),
        new("Consumer Complaint", "4"),
    };

    public static List<SelectListItem> Districts => new()
    {
        new("Dhaka", "1"), new("Chittagong", "2"), new("Rajshahi", "3"),
        new("Khulna", "4"), new("Sylhet", "5"), new("Barisal", "6"),
        // ... enough for the UI to look populated
    };

    // Add more mock objects as needed for each view
}
```

When you finish the real service, replace the `MockData.xxx` return with a real service call.
The view and ViewModel don't need to change — only the controller body does.

## Mobile-first rules (apply to every view)

The target user is on a cheap Android phone with intermittent 4G. Keep these in mind for every
view task:
- Touch targets: minimum 44×44px
- No horizontal scrolling
- Font size: 16px minimum (prevents iOS zoom on input focus)
- Minimal JavaScript
- No heavy images or animations

## Disclaimer policy — 3 mandatory surfaces

1. **Persistent UI banner** (`_DisclaimerBanner.cshtml`) — non-dismissible, no close button, no
   `display: none`, no JS to hide it. Must be visible on every page, always.
2. **Injected into AI responses** — backend-side, via the disclaimer injector.
3. **Stamped into finalized documents/PDFs** — backend-side, at export time.

## XSS caution on AI output

Never `Html.Raw()` any AI-generated text (rights explanations, document drafts). It echoes
citizen free text, so raw rendering is an XSS vector if a prompt injection makes the model emit
HTML/script. Always render it through normal Razor encoding, e.g.:
```html
<div class="rights-explanation document-preview">@Model.RightsExplanation</div>
```
