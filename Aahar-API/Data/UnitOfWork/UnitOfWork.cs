using Microsoft.Identity.Client;
using System;

namespace Aahar_API.Data.UnitOfWork
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AaharDbContext _context;
        
        public UnitOfWork(AaharDbContext context)
        {
            _context = context;
        }

        public async Task<int> CommitAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }

}
