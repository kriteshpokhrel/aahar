using Aahar_API.Models.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace Aahar_API.Models
{
    /// <summary>
    /// Represents a customer order placed at a table, including associated user, table, status, and order items.
    /// </summary>
    public class Order: IWithId
    {
        /// <summary>
        /// Gets or sets the unique identifier for the entity.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the Id of <see cref="Models.Table"/>.
        /// </summary>
        public int TableId { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Models.Table"/>.
        /// </summary>
        public required Table Table { get; set; }

        /// <summary>
        /// Gets or sets Id of the <see cref="Models.User"/>
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Models.User"/>
        /// </summary>
        public required User User { get; set; }

        /// <summary>
        /// Gets or sets the current status of the order.
        /// </summary>
        [Required]
        [MaxLength(30)]
        public required string OrderStatus { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the <see cref="Order"/> was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the collection of items included in the <see cref="Order"/>.
        /// </summary>
        public ICollection<OrderItem>? OrderItems { get; set; }
    }
}
