using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PaymentAPI.Data;
using PaymentAPI.DTOs;
using PaymentAPI.Models;

namespace PaymentAPI.Repositories
{
    public class VoucherRepository : IVoucherRepository
    {
        private readonly PaymentDbContext _context;

        public VoucherRepository(PaymentDbContext context)
        {
            _context = context;
        }

        public async Task<PagedResultDTO<Voucher>> GetPagedAsync(
            int page,
            int limit,
            string? scope,
            bool? isActive,
            string? searchCode,
            int? organizerId,
            bool isOrganizerOnly)
        {
            if (page < 1) page = 1;
            if (limit < 1) limit = 10;

            var query = _context.Vouchers.AsNoTracking().AsQueryable();

            if (isOrganizerOnly && organizerId.HasValue)
            {
                var orgId = organizerId.Value;
                query = query.Where(v => v.OrganizerId == orgId || v.CreatedBy == orgId);
            }
            else if (organizerId.HasValue)
            {
                var orgId = organizerId.Value;
                query = query.Where(v => v.OrganizerId == orgId);
            }

            if (!string.IsNullOrWhiteSpace(scope))
            {
                var trimmedScope = scope.Trim().ToUpperInvariant();
                query = query.Where(v => v.Scope.ToUpper() == trimmedScope);
            }

            if (isActive.HasValue)
            {
                query = query.Where(v => v.IsActive == isActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchCode))
            {
                var search = searchCode.Trim().ToUpperInvariant();
                query = query.Where(v => v.Code.ToUpper().Contains(search));
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(v => v.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();

            return new PagedResultDTO<Voucher>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = limit
            };
        }

        public async Task<Voucher?> GetByIdAsync(int id, bool includeDeleted = false)
        {
            var query = _context.Vouchers.AsQueryable();
            if (includeDeleted)
            {
                query = query.IgnoreQueryFilters();
            }

            return await query.FirstOrDefaultAsync(v => v.VoucherId == id);
        }

        public async Task<Voucher?> GetByCodeAsync(string code)
        {
            var normalizedCode = code.Trim().ToUpperInvariant();
            return await _context.Vouchers.FirstOrDefaultAsync(v => v.Code.ToUpper() == normalizedCode);
        }

        public async Task<bool> CodeExistsAsync(string code, int? excludeId = null)
        {
            var normalizedCode = code.Trim().ToUpperInvariant();
            var query = _context.Vouchers.IgnoreQueryFilters().Where(v => v.Code.ToUpper() == normalizedCode);
            if (excludeId.HasValue)
            {
                query = query.Where(v => v.VoucherId != excludeId.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<Voucher> CreateAsync(Voucher voucher)
        {
            await _context.Vouchers.AddAsync(voucher);
            await _context.SaveChangesAsync();
            return voucher;
        }

        public async Task<Voucher> UpdateAsync(Voucher voucher)
        {
            _context.Vouchers.Update(voucher);
            await _context.SaveChangesAsync();
            return voucher;
        }

        public async Task<List<VoucherUsage>> GetUsagesByVoucherIdAsync(int voucherId)
        {
            return await _context.VoucherUsages
                .AsNoTracking()
                .Where(u => u.VoucherId == voucherId)
                .OrderByDescending(u => u.UsedAt)
                .ToListAsync();
        }
    }
}
