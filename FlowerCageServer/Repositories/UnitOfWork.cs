using FlowerCageServer.Data;

namespace FlowerCageServer.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly FlowerCageDbContext _dbContext;

    public UnitOfWork(FlowerCageDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}
