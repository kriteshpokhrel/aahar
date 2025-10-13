using Aahar_API.Models.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace Aahar_API.Models
{
    /// <summary>
    /// Represents an item available on a Menu.
    /// </summary>
    public class MenuItem : IWithId
    {
        /// <summary>
        /// Gets or sets the unique identifier for the entity.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        [MaxLength(100)]
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the price of the item.
        /// </summary>
        [Required]
        public decimal Price { get; set; }

        /// <summary>
        /// Gets or sets the optional description text, limited to 300 characters.
        /// </summary>
        [MaxLength(300)]
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the resource is currently available for use.
        /// </summary>
        public bool IsAvailable { get; set; } = true;
    }
}
