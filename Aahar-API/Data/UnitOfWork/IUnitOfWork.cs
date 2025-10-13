namespace Aahar_API.Data.UnitOfWork
{
    /// <summary>
    /// Defines a contract for coordinating and committing changes to a data store as a single atomic operation.
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        /// <summary>
        /// Asynchronously commits all pending changes to the underlying data store.
        /// </summary>
        Task<int> CommitAsync();
    }
}
