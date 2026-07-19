using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/testimonials")]
public class AdminTestimonialsController : ControllerBase
{
    private readonly ITestimonialService _service;

    [ActivatorUtilitiesConstructor]
    public AdminTestimonialsController(ITestimonialService service)
    {
        _service = service;
    }

    public AdminTestimonialsController(AppDbContext context)
        : this(new TestimonialService(context))
    {
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        this.ToActionResult(await _service.GetAllAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Testimonial entity) =>
        this.ToActionResult(await _service.CreateAsync(entity));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] Testimonial model) =>
        this.ToActionResult(await _service.UpdateAsync(id, model));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id) =>
        this.ToActionResult(await _service.DeleteAsync(id));
}
