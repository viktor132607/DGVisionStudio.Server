using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/contact")]
public class ContactRequestsController : ControllerBase
{
    private readonly IContactRequestService _service;

    [ActivatorUtilitiesConstructor]
    public ContactRequestsController(IContactRequestService service)
    {
        _service = service;
    }

    public ContactRequestsController(
        AppDbContext context,
        IEmailService emailService,
        IConfiguration configuration)
        : this(new ContactRequestService(context, emailService, configuration))
    {
    }

    [EnableRateLimiting("contact")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateContactRequestDto dto) =>
        this.ToActionResult(await _service.CreateAsync(dto));

    [Authorize(Roles = "Admin")]
    [HttpPut("/api/admin/contact-requests/seen")]
    public async Task<IActionResult> MarkAllSeen() =>
        this.ToActionResult(await _service.MarkAllSeenAsync());
}
