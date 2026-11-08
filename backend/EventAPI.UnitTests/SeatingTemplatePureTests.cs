using System.Text.Json;
using EventAPI.DTOs;
using EventAPI.Models;
using EventAPI.Repositories;
using EventAPI.Services;
using Xunit;

namespace EventAPI.UnitTests;

public class SeatingTemplatePureTests
{
    private class FakeTemplateRepo : ISeatingTemplateRepository
    {
        public List<SeatingTemplate> Store = new();

        public Task<SeatingTemplate?> GetByIdAsync(int id) =>
            Task.FromResult(Store.FirstOrDefault(t => t.SeatingTemplateId == id && !t.IsDeleted));

        public Task<List<SeatingTemplate>> GetVisibleToAsync(int callerId, bool isAdmin) =>
            Task.FromResult(Store.Where(t => !t.IsDeleted && (t.IsPublic || isAdmin || t.OrganizerId == callerId)).ToList());

        public Task<SeatingTemplate> CreateAsync(SeatingTemplate template)
        {
            template.SeatingTemplateId = Store.Count + 1;
            Store.Add(template);
            return Task.FromResult(template);
        }

        public Task UpdateAsync(SeatingTemplate template)
        {
            var existing = Store.FindIndex(t => t.SeatingTemplateId == template.SeatingTemplateId);
            if (existing >= 0) Store[existing] = template;
            return Task.CompletedTask;
        }

        public Task SoftDeleteAsync(int id)
        {
            var t = Store.FirstOrDefault(x => x.SeatingTemplateId == id);
            if (t != null) t.IsDeleted = true;
            return Task.CompletedTask;
        }
    }

    private class FakeTicketTypeRepo : ITicketTypeRepository
    {
        public List<TicketType> Store = new();

        public Task<TicketType?> GetByIdAsync(int id) => Task.FromResult(Store.FirstOrDefault(t => t.TicketTypeId == id));
        public Task<List<TicketType>> GetByEventIdAsync(int eventId) => Task.FromResult(Store.Where(t => t.EventId == eventId).ToList());
        public Task<bool> ExistsNameAsync(int eventId, string typeName, int? excludeId = null) => Task.FromResult(false);
        public Task<TicketType> CreateAsync(TicketType ticketType) => Task.FromResult(ticketType);
        public Task UpdateAsync(TicketType ticketType) => Task.CompletedTask;
        public Task SoftDeleteAsync(int id, int deletedBy) => Task.CompletedTask;
    }

    private class FakeSeatingService : ISeatingService
    {
        public BuildSeatingChartDTO? LastBuildDto;
        public Task<SeatingChartResponseDTO> BuildAsync(int eventId, BuildSeatingChartDTO dto, int callerId, bool isAdmin)
        {
            LastBuildDto = dto;
            return Task.FromResult(new SeatingChartResponseDTO
            {
                EventId = eventId,
                SeatMapId = 1,
                Name = dto.Name,
                Zones = dto.Zones.Select((z, i) => new SeatZoneResponseDTO
                {
                    SeatZoneId = i + 1,
                    ZoneName = z.ZoneName,
                    ZoneType = z.ZoneType,
                    Capacity = z.Capacity ?? (z.Rows * z.SeatsPerRow)
                }).ToList()
            });
        }
        public Task<SeatingChartResponseDTO?> GetByEventIdAsync(int eventId, int? callerId, bool isAdmin) => Task.FromResult<SeatingChartResponseDTO?>(null);
        public Task<SeatingChartPreviewDTO?> GetPreviewAsync(int eventId, int? callerId, bool isAdmin) => Task.FromResult<SeatingChartPreviewDTO?>(null);
        public Task<SeatZoneResponseDTO> GetZoneSeatsAsync(int seatZoneId, int? callerId, bool isAdmin, bool availableOnly = false) => Task.FromResult(new SeatZoneResponseDTO());
        public Task<SeatZoneResponseDTO> AddZoneAsync(int eventId, CreateSeatZoneDTO dto, int callerId, bool isAdmin) => Task.FromResult(new SeatZoneResponseDTO());
        public Task<SeatZoneResponseDTO> UpdateZoneAsync(int seatZoneId, UpdateSeatZoneDTO dto, int callerId, bool isAdmin) => Task.FromResult(new SeatZoneResponseDTO());
        public Task DeleteZoneAsync(int seatZoneId, int callerId, bool isAdmin) => Task.CompletedTask;
        public Task DeleteAsync(int eventId, int callerId, bool isAdmin) => Task.CompletedTask;
    }

    [Fact]
    public async Task UpdateAsync_UpdatesNameDescriptionAndRecalculatesCapacity()
    {
        var templateRepo = new FakeTemplateRepo();
        var initialZones = new List<SeatingTemplateZoneDTO>
        {
            new() { ZoneName = "Zone A", ZoneType = "Seated", Rows = 5, SeatsPerRow = 10 },
            new() { ZoneName = "Standing GA", ZoneType = "Standing", Capacity = 0 }
        };

        var template = new SeatingTemplate
        {
            SeatingTemplateId = 1,
            OrganizerId = 1,
            Name = "Initial Arena",
            Description = "Old desc",
            IsPublic = true,
            ZonesJson = JsonSerializer.Serialize(initialZones)
        };
        templateRepo.Store.Add(template);

        var service = new SeatingTemplateService(
            templateRepo, null!, null!, null!, null!);

        // Update Standing zone capacity from 0 to 100
        var updateDto = new UpdateSeatingTemplateDTO
        {
            Name = "Updated Arena 2026",
            Description = "New modernized venue",
            Zones = new List<SeatingTemplateZoneDTO>
            {
                new() { ZoneName = "Zone A", ZoneType = "Seated", Rows = 5, SeatsPerRow = 10 },
                new() { ZoneName = "Standing GA", ZoneType = "Standing", Capacity = 100 }
            }
        };

        var response = await service.UpdateAsync(1, updateDto, callerId: 1, isAdmin: true);

        Assert.Equal("Updated Arena 2026", response.Name);
        Assert.Equal("New modernized venue", response.Description);
        Assert.Equal(2, response.TotalZones);
        // Capacity: 5*10 + 100 = 150
        Assert.Equal(150, response.EstimatedCapacity);
    }

    [Fact]
    public async Task UpdateAsync_EmptyZonesListThrowsInvalidOperationException()
    {
        var templateRepo = new FakeTemplateRepo();
        var template = new SeatingTemplate
        {
            SeatingTemplateId = 1,
            OrganizerId = 1,
            Name = "Arena",
            ZonesJson = JsonSerializer.Serialize(new List<SeatingTemplateZoneDTO>
            {
                new() { ZoneName = "Zone A", ZoneType = "Seated", Rows = 2, SeatsPerRow = 5 }
            })
        };
        templateRepo.Store.Add(template);

        var service = new SeatingTemplateService(templateRepo, null!, null!, null!, null!);

        var updateDto = new UpdateSeatingTemplateDTO
        {
            Zones = new List<SeatingTemplateZoneDTO>()
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(1, updateDto, callerId: 1, isAdmin: true));
    }

    [Fact]
    public async Task UpdateAsync_NonOwnerNonAdminCannotUpdateTemplate()
    {
        var templateRepo = new FakeTemplateRepo();
        var template = new SeatingTemplate
        {
            SeatingTemplateId = 1,
            OrganizerId = 10, // Owned by user 10
            Name = "Organizer's Private Template",
            ZonesJson = "[]"
        };
        templateRepo.Store.Add(template);

        var service = new SeatingTemplateService(templateRepo, null!, null!, null!, null!);

        var updateDto = new UpdateSeatingTemplateDTO { Name = "Hacked Template" };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.UpdateAsync(1, updateDto, callerId: 99, isAdmin: false));
    }

    [Fact]
    public async Task ApplyToEventAsync_CanExcludeUnneededZonesAndMapOnlyActiveSubset()
    {
        var templateRepo = new FakeTemplateRepo();
        var ticketRepo = new FakeTicketTypeRepo();
        var seatingService = new FakeSeatingService();

        var templateZones = new List<SeatingTemplateZoneDTO>
        {
            new() { ZoneName = "Zone 1 (VIP)", ZoneType = "Seated", Rows = 2, SeatsPerRow = 5 },
            new() { ZoneName = "Zone 2 (Standard)", ZoneType = "Seated", Rows = 4, SeatsPerRow = 10 },
            new() { ZoneName = "Zone 3 (Standing)", ZoneType = "Standing", Capacity = 100 }
        };

        var template = new SeatingTemplate
        {
            SeatingTemplateId = 42,
            OrganizerId = 10,
            Name = "3-Zone Stadium Template",
            IsPublic = true,
            ZonesJson = JsonSerializer.Serialize(templateZones)
        };
        templateRepo.Store.Add(template);

        // Concert has ticket types 101 and 102
        ticketRepo.Store.Add(new TicketType { TicketTypeId = 101, EventId = 55, TypeName = "VIP Tier" });
        ticketRepo.Store.Add(new TicketType { TicketTypeId = 102, EventId = 55, TypeName = "Standing GA" });

        var service = new SeatingTemplateService(
            templateRepo, null!, null!, ticketRepo, seatingService);

        // Organizer excludes Zone 2 (index 1), only applies Zone 1 (index 0) and Zone 3 (index 2)
        var applyDto = new ApplySeatingTemplateDTO
        {
            ZoneMappings = new List<TemplateZoneMappingDTO>
            {
                new() { ZoneIndex = 0, TicketTypeId = 101, ZoneName = "Custom VIP" },
                new() { ZoneIndex = 2, TicketTypeId = 102, ZoneName = "Custom Standing", Capacity = 100 }
            }
        };

        var result = await service.ApplyToEventAsync(42, 55, applyDto, callerId: 10, isAdmin: false);

        Assert.NotNull(result);
        Assert.NotNull(seatingService.LastBuildDto);
        // Only 2 zones were mapped and built
        Assert.Equal(2, seatingService.LastBuildDto.Zones.Count);
        Assert.Equal("Custom VIP", seatingService.LastBuildDto.Zones[0].ZoneName);
        Assert.Equal(101, seatingService.LastBuildDto.Zones[0].TicketTypeId);
        Assert.Equal("Custom Standing", seatingService.LastBuildDto.Zones[1].ZoneName);
        Assert.Equal(102, seatingService.LastBuildDto.Zones[1].TicketTypeId);
    }

    [Fact]
    public async Task ApplyToEventAsync_InvalidTicketTypeThrowsArgumentException()
    {
        var templateRepo = new FakeTemplateRepo();
        var ticketRepo = new FakeTicketTypeRepo();
        var seatingService = new FakeSeatingService();

        var template = new SeatingTemplate
        {
            SeatingTemplateId = 1,
            OrganizerId = 10,
            IsPublic = true,
            ZonesJson = JsonSerializer.Serialize(new List<SeatingTemplateZoneDTO>
            {
                new() { ZoneName = "Zone 1", ZoneType = "Seated", Rows = 1, SeatsPerRow = 10 }
            })
        };
        templateRepo.Store.Add(template);

        var service = new SeatingTemplateService(
            templateRepo, null!, null!, ticketRepo, seatingService);

        var applyDto = new ApplySeatingTemplateDTO
        {
            ZoneMappings = new List<TemplateZoneMappingDTO>
            {
                new() { ZoneIndex = 0, TicketTypeId = 9999 } // Ticket type 9999 doesn't belong to event
            }
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ApplyToEventAsync(1, 55, applyDto, callerId: 10, isAdmin: false));
    }
}
