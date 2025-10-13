using Aahar_API.Models;
using Aahar_API.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Aahar_API.Repository.Implementations
{
    /// <summary>
    /// Represents repository layer for User entity.
    /// </summary>
    public class UserRepository : BaseRepository<User>, IUserRepository
    {
        /// <summary>
        /// Represents repository layer for User entity.
        /// </summary>
        /// <param name="context"></param>
        public UserRepository(DbContext context) : base(context)
        {
        }
    }
}
