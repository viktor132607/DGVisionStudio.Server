using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/testimonials")]
public class TestimonialsController : ControllerBase
{
    private readonly ITestimonialService _service;

    [ActivatorUtilitiesConstructor]
    public TestimonialsController(ITestimonialService service)
    {
        _service = service;
    }

    public TestimonialsController(AppDbContext context)
        : this(new TestimonialService(context))
    {
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        this.ToActionResult(await _service.GetPublishedAsync());
}
