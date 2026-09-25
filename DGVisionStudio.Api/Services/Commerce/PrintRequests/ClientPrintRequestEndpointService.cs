using System.Security.Claims;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs.PrintRequests;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Api.Services;

public sealed class ClientPrintRequestEndpointService :
    IClientPrintRequestEndpointService
{
    private readonly ClientPrintRequestQueryService _queries;
    private readonly ClientPrintRequestCreationService _creation;

    [ActivatorUtilitiesConstructor]
    public ClientPrintRequestEndpointService(
        ClientPrintRequestQueryService queries,
        ClientPrintRequestCreationService creation)
    {
        _queries = queries;
        _creation = creation;
    }

    public ClientPrintRequestEndpointService(AppDbContext context)
        : this(
            new ClientPrintRequestQueryService(
                context,
                new ClientPrintRequestUserContextService(),
                new ClientPrintRequestMapper()),
            new ClientPrintRequestCreationService(
                context,
                new ClientPrintRequestUserContextService(),
                new ClientPrintRequestMapper()))
    {
    }

    public Task<ControllerServiceResult> GetMineAsync(
        ClaimsPrincipal principal) =>
        _queries.GetMineAsync(principal);

    public Task<ControllerServiceResult> GetMineByIdAsync(
        ClaimsPrincipal principal,
        int id) =>
        _queries.GetMineByIdAsync(principal, id);

    public Task<ControllerServiceResult> CreateAsync(
        ClaimsPrincipal principal,
        CreatePrintRequestDto dto) =>
        _creation.CreateAsync(principal, dto);
}

public sealed record CreatedPrintRequestResult(int Id);
