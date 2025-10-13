using Aahar_API.Models.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace Aahar_API.Models
{
    /// <summary>
    /// Represents an individual item within a customer order, including the selected menu item, quantity, and any
    /// associated notes.
    /// </summary>
    public class OrderItem : IWithId
    {
        /// <summary>
        /// Gets or sets the unique identifier for the entity.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the Id of <see cref="Aahar_API.Models.Order"/>.
        /// </summary>
        public int OrderId { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Models.Order"/>.
        /// </summary>
        public required Order Order { get; set; }

        /// <summary>
        /// Gets or sets Id of <see cref="MenuItem"/>.
        /// </summary>
        public int MenuItemId { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Models.MenuItem"/>>.
        /// </summary>
        public required MenuItem MenuItem { get; set; }

        /// <summary>
        /// Gets or sets the quantity of <see cref="Models.MenuItem"/>.
        /// </summary>
        [Required]
        public int Quantity { get; set; }

        /// <summary>
        /// Gets or sets optional notes of this Order Item.
        /// </summary>
        [MaxLength(300)]
        public string? Notes { get; set; }
    }
}
