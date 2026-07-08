using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Tests.Content;

public sealed class ServicesAndTestimonialsControllerTests
{
    [Fact]
    public async Task Services_GetAll_ReturnsOnlyActiveServices_OrderedByDisplayOrderThenId()
    {
        await using var context = CreateContext();
        context.Services.AddRange(
            new Service { Title = "Inactive", Description = "Inactive", DisplayOrder = 1, IsActive = false },
            new Service { Title = "Second", Description = "Second", DisplayOrder = 2, IsActive = true },
            new Service { Title = "First", Description = "First", DisplayOrder = 1, IsActive = true });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var controller = new ServicesController(context);

        var result = await controller.GetAll();

        var okResult = result.Should().BeOfType<OkObjectResult>().Which;
        var services = okResult.Value.Should().BeAssignableTo<IEnumerable<Service>>().Which.ToList();
        services.Select(x => x.Title).Should().Equal("First", "Second");
        services.Should().OnlyContain(x => x.IsActive);
    }

    [Fact]
    public async Task Services_GetById_ReturnsNotFound_WhenServiceDoesNotExist()
    {
        await using var context = CreateContext();
        var controller = new ServicesController(context);

        var result = await controller.GetById(404);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task AdminServices_Create_ReturnsBadRequest_WhenTitleIsMissing()
    {
        await using var context = CreateContext();
        var controller = new AdminServicesController(context);

        var result = await controller.Create(new ServiceCardDto
        {
            Title = " ",
            Description = "Description",
            IsActive = true
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        context.Services.Should().BeEmpty();
    }

    [Fact]
    public async Task AdminServices_Create_AssignsNextDisplayOrder()
    {
        await using var context = CreateContext();
        context.Services.Add(new Service { Title = "Existing", Description = "Existing", DisplayOrder = 3, IsActive = true });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var controller = new AdminServicesController(context);

        var result = await controller.Create(new ServiceCardDto
        {
            Title = " New Service ",
            ShortDescription = " Short ",
            Description = " Description ",
            CoverImageUrl = " /cover.jpg ",
            IsActive = true
        });

        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Which;
        var service = createdResult.Value.Should().BeOfType<Service>().Which;
        service.Title.Should().Be("New Service");
        service.ShortDescription.Should().Be("Short");
        service.Description.Should().Be("Description");
        service.CoverImageUrl.Should().Be("/cover.jpg");
        service.DisplayOrder.Should().Be(4);
    }

    [Fact]
    public async Task AdminServices_Reorder_ReordersSpecifiedServices_AndAppendsRemaining()
    {
        await using var context = CreateContext();
        context.Services.AddRange(
            new Service { Title = "One", Description = "One", DisplayOrder = 1 },
            new Service { Title = "Two", Description = "Two", DisplayOrder = 2 },
            new Service { Title = "Three", Description = "Three", DisplayOrder = 3 });
        await context.SaveChangesAsync();
        var services = await context.Services.OrderBy(x => x.DisplayOrder).ToListAsync();
        context.ChangeTracker.Clear();

        var controller = new AdminServicesController(context);

        var result = await controller.Reorder(new ReorderServicesDto { Ids = [services[2].Id, services[0].Id] });

        var okResult = result.Should().BeOfType<OkObjectResult>().Which;
        var reordered = okResult.Value.Should().BeAssignableTo<IEnumerable<Service>>().Which.ToList();
        reordered.Select(x => x.Title).Should().Equal("Three", "One", "Two");
        reordered.Select(x => x.DisplayOrder).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Testimonials_GetAll_ReturnsOnlyPublishedTestimonials_OrderedByDisplayOrderThenCreatedDescending()
    {
        await using var context = CreateContext();
        context.Testimonials.AddRange(
            new Testimonial { ClientName = "Hidden", Content = "Hidden", DisplayOrder = 1, IsPublished = false },
            new Testimonial { ClientName = "Older", Content = "Older", DisplayOrder = 1, IsPublished = true, CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Testimonial { ClientName = "Newer", Content = "Newer", DisplayOrder = 1, IsPublished = true, CreatedAtUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc) });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var controller = new TestimonialsController(context);

        var result = await controller.GetAll();

        var okResult = result.Should().BeOfType<OkObjectResult>().Which;
        var testimonials = okResult.Value.Should().BeAssignableTo<IEnumerable<Testimonial>>().Which.ToList();
        testimonials.Select(x => x.ClientName).Should().Equal("Newer", "Older");
        testimonials.Should().OnlyContain(x => x.IsPublished);
    }

    [Fact]
    public async Task AdminTestimonials_Update_ReturnsNotFound_WhenTestimonialDoesNotExist()
    {
        await using var context = CreateContext();
        var controller = new AdminTestimonialsController(context);

        var result = await controller.Update(404, new Testimonial { ClientName = "Client", Content = "Content" });

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task AdminTestimonials_Delete_ReturnsNotFound_WhenTestimonialDoesNotExist()
    {
        await using var context = CreateContext();
        var controller = new AdminTestimonialsController(context);

        var result = await controller.Delete(404);

        result.Should().BeOfType<NotFoundResult>();
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
