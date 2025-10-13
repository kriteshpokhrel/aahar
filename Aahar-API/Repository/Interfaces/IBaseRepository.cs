using Aahar_API.Models.Interfaces;

namespace Aahar_API.Repository.Interfaces
{
    /// <summary>
    /// Defines a generic contract for basic data access operations on entities with an identifier.
    /// </summary>
    /// <typeparam name="T">The type of entity managed by the repository. Must implement <see cref="IWithId"/> to ensure the presence of an
    /// identifier.</typeparam>
    public interface IBaseRepository<T> where T : IWithId
    {
        /// <summary>
        /// Asynchronously retrieves an entity by its id.
        /// </summary>
        /// <param name="id">The id of the entity to retrieve.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the entity of type T if found;
        /// otherwise, null.</returns>
        Task<T?> GetByIdAsync(int id);

        /// <summary>
        /// Asynchronously retrieves all entities of type <typeparamref name="T"/> from the data source.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains an enumerable collection of all
        /// entities of type <typeparamref name="T"/>. The collection will be empty if no entities are found.</returns>
        Task<IEnumerable<T>> GetAllAsync();
        
        /// <summary>
        /// Asynchronously adds the specified entity to the data store.
        /// </summary>
        /// <param name="entity">The entity to add. Cannot be null.</param>
        /// <returns>A task that represents the asynchronous add operation.</returns>
        Task AddAsync(T entity);

        /// <summary>
        /// Asynchronously updates the specified entity in the data store.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        Task UpdateAsync(T entity);
        
        /// <summary>
        /// Asynchronously deletes the entity with the specified identifier.
        /// </summary>
        /// <param name="id">The Id of the entity to delete. Must be a valid identifier for an existing entity.</param>
        /// <returns>A task that represents the asynchronous delete operation.</returns>
        Task DeleteAsync(int id);

        /// <summary>
        /// Asynchronously determines whether an entity with the specified identifier exists.
        /// </summary>
        /// <param name="id">The Id of the entity to check for existence.</param>
        /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the entity
        /// exists; otherwise, <see langword="false"/>.</returns>
        Task<bool> ExistsAsync(int id);
    }

}
