using Aahar.Application;
using Aahar.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aahar.Infrastructure;

public sealed class MenuRepository(RestaurantDbContext db) : IMenuRepository
{
    public Task<List<MenuItem>> ListAsync(bool availableOnly, CancellationToken ct) =>
        db.MenuItems.AsNoTracking().Where(x => !availableOnly || x.IsAvailable).OrderBy(x => x.Name).ToListAsync(ct);
    public async Task<MenuItem?> FindAsync(int id, CancellationToken ct) => await db.MenuItems.FindAsync([id], ct);
    public Task<bool> ExistsAsync(int id, bool availableOnly, CancellationToken ct) =>
        db.MenuItems.AsNoTracking().AnyAsync(x => x.Id == id && (!availableOnly || x.IsAvailable), ct);
    public Task<Dictionary<int, MenuItem>> FindByIdsAsync(int[] ids, CancellationToken ct) =>
        db.MenuItems.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
    public async Task<Dictionary<int, MenuItem>> LockForShareAsync(IEnumerable<int> menuIds, CancellationToken ct)
    {
        var ids = menuIds.Distinct().Order().ToArray();
        return (await db.MenuItems.FromSqlInterpolated(
            $"SELECT * FROM \"MenuItems\" WHERE \"Id\" = ANY({ids}) ORDER BY \"Id\" FOR SHARE").ToListAsync(ct))
            .ToDictionary(x => x.Id);
    }
    public void Add(MenuItem item) => db.MenuItems.Add(item);
    public Task<List<RecipeLine>> RecipeAsync(int menuId, CancellationToken ct) =>
        db.Recipes.AsNoTracking().Where(x => x.MenuItemId == menuId).OrderBy(x => x.IngredientId)
            .Select(x => new RecipeLine(x.IngredientId, x.Quantity)).ToListAsync(ct);
    public Task<MenuItem?> FindWithRecipeAsync(int menuId, CancellationToken ct) =>
        db.MenuItems.Include(x => x.Recipe).SingleOrDefaultAsync(x => x.Id == menuId, ct);
    public Task<List<RecipeIngredient>> RecipesForMenuAsync(int[] ids, CancellationToken ct) =>
        db.Recipes.Include(x => x.Ingredient).Where(x => ids.Contains(x.MenuItemId)).ToListAsync(ct);
    public void RemoveRecipes(IEnumerable<RecipeIngredient> recipes) => db.Recipes.RemoveRange(recipes);
    public void AddRecipes(IEnumerable<RecipeIngredient> recipes) => db.Recipes.AddRange(recipes);
}
