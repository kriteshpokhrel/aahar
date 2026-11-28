using Aahar.Domain;

namespace Aahar.Application;

public interface IMenuRepository
{
    Task<List<MenuItem>> ListAsync(bool availableOnly, CancellationToken ct);
    Task<MenuItem?> FindAsync(int id, CancellationToken ct);
    Task<bool> ExistsAsync(int id, bool availableOnly, CancellationToken ct);
    Task<Dictionary<int, MenuItem>> FindByIdsAsync(int[] ids, CancellationToken ct);
    Task<Dictionary<int, MenuItem>> LockForShareAsync(IEnumerable<int> ids, CancellationToken ct);
    void Add(MenuItem item);
    Task<List<RecipeLine>> RecipeAsync(int menuId, CancellationToken ct);
    Task<MenuItem?> FindWithRecipeAsync(int menuId, CancellationToken ct);
    Task<List<RecipeIngredient>> RecipesForMenuAsync(int[] ids, CancellationToken ct);
    void RemoveRecipes(IEnumerable<RecipeIngredient> recipes);
    void AddRecipes(IEnumerable<RecipeIngredient> recipes);
}
