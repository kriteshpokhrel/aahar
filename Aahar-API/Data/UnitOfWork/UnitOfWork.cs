using Microsoft.Identity.Client;
using System;

namespace Aahar_API.Data.UnitOfWork
{
    /// <inheritdoc/>
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AaharDbContext _context;

        /// <inheritdoc/>
        public UnitOfWork(AaharDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public async Task<int> CommitAsync()
        {
            return await _context.SaveChangesAsync();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _context.Dispose();
        }
    }

}
