using Aahar_API.Models.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace Aahar_API.Models
{
    /// <summary>
    /// Represents an application user.
    /// </summary>
    public class User : IWithId
    {
        /// <summary>
        /// Gets or sets the unique identifier for the entity.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the Name of the User.
        /// </summary>
        [MaxLength(100)]
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the email address associated with the User.
        /// </summary>
        [MaxLength(200)]
        public required string Email { get; set; }

        /// <summary>
        /// Gets or sets the hashed representation of the user's password.
        /// </summary>
        /// <remarks>The password hash is limited to a maximum length of 256 characters. This property
        /// should contain a securely generated hash rather than a plain-text password.</remarks>
        [MaxLength(256)]
        public required string PasswordHash{ get; set; }

        /// <summary>
        /// Gets or sets the role assigned to the user or entity.
        /// </summary>
        [MaxLength(20)]
        public required string Role { get; set; }
    }
}
