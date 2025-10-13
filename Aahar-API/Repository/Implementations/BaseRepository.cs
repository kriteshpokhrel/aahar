using Aahar_API.Models.Interfaces;
using Aahar_API.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Aahar_API.Repository.Implementations
{
    /// <inheritdoc/>
    public class BaseRepository<T> : IBaseRepository<T> where T : class, IWithId
    {
        private readonly DbContext _context;
        private readonly DbSet<T> _dbSet;

        /// <inheritdoc/>
        public BaseRepository(DbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        /// <inheritdoc/>
        public async Task<T?> GetByIdAsync(int id)
        {
            return await _dbSet.FindAsync(id);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet.ToListAsync();
        }

        /// <inheritdoc/>
        public async Task AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
        }

        /// <inheritdoc/>
        public async Task UpdateAsync(T entity)
        {
            _dbSet.Update(entity);
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(int id)
        {
            var entity = await GetByIdAsync(id);
            if (entity != null)
            {
                _dbSet.Remove(entity);
            }
        }

        /// <inheritdoc/>
        public async Task<bool> ExistsAsync(int id)
        {
            return await _dbSet.AnyAsync(e => e.Id == id);
        }
    }
}
