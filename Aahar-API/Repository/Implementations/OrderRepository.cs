using Aahar_API.Data;
using Aahar_API.Repository.Interfaces;

namespace Aahar_API.Repository.Implementations
{
    /// <summary>
    /// Represents repository layer for Order entity.
    /// </summary>
    public class OrderRepository: BaseRepository<Models.Order>, IOrderRepository
    {
        /// <summary>
        /// Represents repository layer for Order entity.
        /// </summary>
        /// <param name="context"></param>
        public OrderRepository(AaharDbContext context) : base(context)
        {
        }
    }
}
