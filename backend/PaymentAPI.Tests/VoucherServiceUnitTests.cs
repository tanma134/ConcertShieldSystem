using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using PaymentAPI.API;
using PaymentAPI.DTOs;
using PaymentAPI.Models;
using PaymentAPI.Repositories;
using PaymentAPI.Services;
using Xunit;

namespace PaymentAPI.Tests
{
    public class VoucherServiceUnitTests
    {
        private readonly Mock<IVoucherRepository> _mockRepo = new();
        private readonly Mock<IEventApiClient> _mockEventApi = new();
        private readonly Mock<ITicketApiClient> _mockTicketApi = new();

        private VoucherService CreateService()
        {
            return new VoucherService(_mockRepo.Object, _mockEventApi.Object, _mockTicketApi.Object);
        }

        #region UC_7.6 / UC_7.9: Create Voucher Tests

        [Fact]
        public async Task CreateVoucherAsync_UTCID01_NormalPercent_Success()
        {
            // Arrange
            var svc = CreateService();
            var dto = new CreateVoucherDTO
            {
                Code = "SUMMER20",
                Scope = "ORGANIZER",
                OrganizerId = 10,
                DiscountType = "PERCENT",
                DiscountPercent = 20,
                TotalQuantity = 100,
                StartsAt = DateTime.UtcNow.AddMinutes(5),
                EndsAt = DateTime.UtcNow.AddDays(30)
            };

            _mockRepo.Setup(r => r.CodeExistsAsync("SUMMER20")).ReturnsAsync(false);
            _mockRepo.Setup(r => r.AddAsync(It.IsAny<Voucher>())).Returns(Task.CompletedTask);

            // Act
            var result = await svc.CreateVoucherAsync(dto, currentUserId: 10, isAdmin: false, isOrganizer: true);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("SUMMER20", result.Code);
            Assert.Equal(20, result.DiscountPercent);
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<Voucher>()), Times.Once);
        }

        [Fact]
        public async Task CreateVoucherAsync_UTCID06_DuplicateCode_ThrowsInvalidOperationException()
        {
            // Arrange
            var svc = CreateService();
            var dto = new CreateVoucherDTO
            {
                Code = "EXISTING_CODE",
                Scope = "ORGANIZER",
                OrganizerId = 10,
                DiscountType = "PERCENT",
                DiscountPercent = 10,
                TotalQuantity = 50,
                StartsAt = DateTime.UtcNow.AddMinutes(1),
                EndsAt = DateTime.UtcNow.AddDays(10)
            };

            _mockRepo.Setup(r => r.CodeExistsAsync("EXISTING_CODE")).ReturnsAsync(true);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.CreateVoucherAsync(dto, currentUserId: 10, isAdmin: false, isOrganizer: true));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("CODE WITH SPACE")]
        public async Task CreateVoucherAsync_UTCID07_EmptyOrInvalidCode_ThrowsArgumentException(string code)
        {
            // Arrange
            var svc = CreateService();
            var dto = new CreateVoucherDTO
            {
                Code = code,
                Scope = "ORGANIZER",
                OrganizerId = 10,
                DiscountType = "FIXED_AMOUNT",
                DiscountAmount = 50000,
                TotalQuantity = 100,
                StartsAt = DateTime.UtcNow.AddMinutes(1),
                EndsAt = DateTime.UtcNow.AddDays(10)
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                svc.CreateVoucherAsync(dto, currentUserId: 10, isAdmin: false, isOrganizer: true));
        }

        [Fact]
        public async Task CreateVoucherAsync_UTCID10_NonAdminCreateSystemVoucher_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var svc = CreateService();
            var dto = new CreateVoucherDTO
            {
                Code = "PLATFORM10",
                Scope = "SYSTEM",
                DiscountType = "PERCENT",
                DiscountPercent = 10,
                TotalQuantity = 500,
                StartsAt = DateTime.UtcNow.AddMinutes(1),
                EndsAt = DateTime.UtcNow.AddDays(10)
            };

            _mockRepo.Setup(r => r.CodeExistsAsync("PLATFORM10")).ReturnsAsync(false);

            // Act & Assert (isOrganizer=true, isAdmin=false)
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                svc.CreateVoucherAsync(dto, currentUserId: 10, isAdmin: false, isOrganizer: true));
        }

        #endregion

        #region UC_7.5 / UC_7.8: Get Vouchers Tests

        [Fact]
        public async Task GetVouchersAsync_UTCID01_NormalPagination_ReturnsPagedResult()
        {
            // Arrange
            var svc = CreateService();
            var mockVouchers = new List<Voucher>
            {
                new() { VoucherId = 1, Code = "V1", Scope = "ORGANIZER", OrganizerId = 10, IsActive = true },
                new() { VoucherId = 2, Code = "V2", Scope = "ORGANIZER", OrganizerId = 10, IsActive = true }
            };

            _mockRepo.Setup(r => r.GetPagedAsync(1, 10, "ORGANIZER", null, null, 10, true))
                     .ReturnsAsync(new PagedResultDTO<Voucher>
                     {
                         Items = mockVouchers,
                         TotalCount = 2,
                         Page = 1,
                         PageSize = 10,
                         TotalPages = 1
                     });

            // Act
            var result = await svc.GetVouchersAsync(page: 1, limit: 10, scope: "ORGANIZER", isActive: null, searchCode: null, organizerId: 10, userId: 10, isAdmin: false, isOrg: true);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.TotalCount);
            Assert.Equal(2, result.Items.Count);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task GetVouchersAsync_UTCID08_InvalidPage_ThrowsArgumentException(int page)
        {
            // Arrange
            var svc = CreateService();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                svc.GetVouchersAsync(page: page, limit: 10, scope: "ORGANIZER", isActive: null, searchCode: null, organizerId: 10, userId: 10, isAdmin: false, isOrg: true));
        }

        #endregion

        #region UC_7.11: Soft Delete Voucher Tests

        [Fact]
        public async Task SoftDeleteVoucherAsync_UTCID01_NormalDelete_Success()
        {
            // Arrange
            var svc = CreateService();
            var existing = new Voucher
            {
                VoucherId = 15,
                Code = "SYS15",
                Scope = "SYSTEM",
                IsDeleted = false
            };

            _mockRepo.Setup(r => r.GetByIdAsync(15)).ReturnsAsync(existing);
            _mockRepo.Setup(r => r.UpdateAsync(It.IsAny<Voucher>())).Returns(Task.CompletedTask);

            // Act
            var result = await svc.SoftDeleteVoucherAsync(15, currentUserId: 1, isAdmin: true, isOrganizer: false);

            // Assert
            Assert.True(result);
            Assert.True(existing.IsDeleted);
            _mockRepo.Verify(r => r.UpdateAsync(existing), Times.Once);
        }

        [Fact]
        public async Task SoftDeleteVoucherAsync_UTCID05_NotFound_ThrowsKeyNotFoundException()
        {
            // Arrange
            var svc = CreateService();
            _mockRepo.Setup(r => r.GetByIdAsync(99999)).ReturnsAsync((Voucher?)null);

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                svc.SoftDeleteVoucherAsync(99999, currentUserId: 1, isAdmin: true, isOrganizer: false));
        }

        [Fact]
        public async Task SoftDeleteVoucherAsync_UTCID08_NonAdminDeleteSystemVoucher_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var svc = CreateService();
            var sysVoucher = new Voucher
            {
                VoucherId = 15,
                Code = "SYS15",
                Scope = "SYSTEM",
                IsDeleted = false
            };

            _mockRepo.Setup(r => r.GetByIdAsync(15)).ReturnsAsync(sysVoucher);

            // Act & Assert (isOrganizer=true, isAdmin=false)
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                svc.SoftDeleteVoucherAsync(15, currentUserId: 10, isAdmin: false, isOrganizer: true));
        }

        #endregion
    }
}
