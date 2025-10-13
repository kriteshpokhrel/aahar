namespace Aahar_API.Models.Interfaces
{
    /// <summary>
    /// Represents an entity with Id.
    /// </summary>
    public interface IWithId
    {
        /// <summary>
        /// Gets or sets Id of the entity.
        /// </summary>
        public int Id { get; set; }
    }
}
