using FluentAssertions;
using Humanitarian.Application.Common;
using Humanitarian.Application.Resources;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;
using Humanitarian.Tests.Support;
using Xunit;

namespace Humanitarian.Tests;

public class ResourceServiceTests
{
    private readonly InMemoryUnitOfWork _uow = new();
    private readonly FakeCurrentUser _user = new();
    private readonly RecordingAuditService _audit = new();
    private readonly ResourceService _sut;

    public ResourceServiceTests()
    {
        _sut = new ResourceService(_uow, _audit, _user, TimeProvider.System);
        _user.Become(Guid.NewGuid(), "logistics@demo.org", Roles.LogisticsManager);
    }

    private static CreateResourceDto Rice(int total = 100, int reorder = 20, double? lat = 6.93, double? lon = 79.85) =>
        new("Rice packs", ResourceCategory.Food, "packs", total, reorder, "Colombo depot", lat, lon, null);

    [Fact]
    public async Task Create_StartsWithAllStockAvailable_AndIsAudited()
    {
        var dto = await _sut.CreateAsync(Rice());

        dto.QuantityTotal.Should().Be(100);
        dto.QuantityAvailable.Should().Be(100);
        dto.UtilisationPercent.Should().Be(0);
        dto.IsLowStock.Should().BeFalse();
        _audit.Entries.Should().Contain(e => e.Action == "ResourceCreated");
    }

    [Fact]
    public async Task Restock_IncreasesTotalAndAvailable_AndClearsLowStock()
    {
        var created = await _sut.CreateAsync(Rice(total: 15, reorder: 20));
        created.IsLowStock.Should().BeTrue();

        var restocked = await _sut.RestockAsync(created.Id, new RestockDto(100, "Delivery from partner"));

        restocked.QuantityTotal.Should().Be(115);
        restocked.QuantityAvailable.Should().Be(115);
        restocked.IsLowStock.Should().BeFalse();
        _audit.Entries.Should().Contain(e => e.Action == "ResourceRestocked");
    }

    [Fact]
    public async Task ListAsync_CanFilterByCategoryAndLowStock()
    {
        await _sut.CreateAsync(Rice(total: 10, reorder: 20));
        await _sut.CreateAsync(new CreateResourceDto("Water bottles", ResourceCategory.Water, "bottles", 500, 50, null, null, null, null));

        (await _sut.ListAsync(null, false)).Should().HaveCount(2);
        (await _sut.ListAsync(ResourceCategory.Water, false)).Should().ContainSingle();
        (await _sut.ListAsync(null, true)).Should().ContainSingle().Which.Name.Should().Be("Rice packs");
    }

    [Fact]
    public async Task Citizen_CannotCreateOrViewInventory()
    {
        _user.Become(Guid.NewGuid(), "citizen@demo.org", Roles.Citizen);

        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.CreateAsync(Rice()));
        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.ListAsync(null, false));
    }

    [Fact]
    public async Task CaseManager_CanViewButNotChangeInventory()
    {
        var created = await _sut.CreateAsync(Rice());
        _user.Become(Guid.NewGuid(), "casemanager@demo.org", Roles.CaseManager);

        (await _sut.GetAsync(created.Id)).Name.Should().Be("Rice packs");
        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.RestockAsync(created.Id, new RestockDto(5, null)));
    }

    [Fact]
    public async Task Create_WithOnlyLatitude_IsRejected()
    {
        await Assert.ThrowsAsync<AppValidationException>(() => _sut.CreateAsync(Rice(lat: 6.9, lon: null)));
    }
}
