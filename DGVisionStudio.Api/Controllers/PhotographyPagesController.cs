using System.Xml.Linq;
using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/photography-pages")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PhotographyPagesController(PhotographyPageService service) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List() => Ok(await service.ListAsync());
    [HttpGet("{slug}/albums")] public async Task<IActionResult> Albums(string slug) =>
        this.ToActionResult(await service.AlbumsAsync(slug));
    [HttpGet("sitemap.xml")] public async Task<IActionResult> Sitemap()
    {
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var pages = await service.ListAsync();
        return Content(new XDocument(new XElement(ns + "urlset", pages.Select(p =>
            new XElement(ns + "url", new XElement(ns + "loc", "https://dgvisionstudio.com/fotograf-ruse" +
                (p.Slug.Length == 0 ? "" : "/" + p.Slug)))))).ToString(), "application/xml");
    }
}
