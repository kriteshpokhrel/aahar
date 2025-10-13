using Aahar_API.Data;
using Aahar_API.Repository.Interfaces;

namespace Aahar_API.Repository.Implementations
{
    /// <summary>
    /// Represents repository layer for Order entity.
    /// </summary>
    public class OrderItemRepository: BaseRepository<Models.OrderItem>, IOrderItemRepository
    {
        /// <summary>
        /// Represents repository layer for Order entity.
        /// </summary>
        /// <param name="context"></param>
        public OrderItemRepository(AaharDbContext context) : base(context)
        {
        }
    }
}
