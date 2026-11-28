using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Aahar.Infrastructure;

public static class DatabaseConflicts
{
    public static bool IsConflict(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException
                or PostgresException { SqlState: "23505" or "40001" or "40P01" })
                return true;
        }
        return false;
    }
}
