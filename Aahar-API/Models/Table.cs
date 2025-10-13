using Aahar_API.Models.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aahar_API.Models
{
    /// <summary>
    /// Represents a Table within a restaurant.
    /// </summary>
    [Table("DiningTable")]
    public class Table : IWithId
    {
        /// <summary>
        /// Represents the unique identifier for the dining table.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the Dining Table Number.
        /// </summary>
        [Required]
        public int Number { get; set; }

        /// <summary>
        /// Gets or sets the QR code value associated the Table.
        /// </summary>
        [MaxLength(100)]
        public required string QRCode{ get; set; }

        /// <summary>
        /// Gets or sets the current status of Table.
        /// </summary>
        [MaxLength(20)]
        public required string Status { get; set; }
    }
}
