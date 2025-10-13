using Aahar_API.Data;
using Aahar_API.Repository.Interfaces;

namespace Aahar_API.Repository.Implementations
{
    /// <summary>
    /// Represents repository layer for Order Item entity.
    /// </summary>
    public class MenuItemRepository: BaseRepository<Models.MenuItem>, IMenuItemRepository
    {
        /// <summary>
        /// Represents repository layer for Order Item entity.
        /// </summary>
        /// <param name="context"></param>
        public MenuItemRepository(AaharDbContext context) : base(context)
        {
        }
    }
}
