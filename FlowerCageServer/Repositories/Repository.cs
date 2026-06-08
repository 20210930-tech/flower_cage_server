using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using FlowerCageServer.Data;
using FlowerCageServer.Entities;

namespace FlowerCageServer.Repositories;

public class Repository<TEntity> : IRepository<TEntity> where TEntity : BaseEntity
{
    private readonly DbSet<TEntity> _dbSet;

    public Repository(FlowerCageDbContext dbContext)
    {
        _dbSet = dbContext.Set<TEntity>();
    }

    public IQueryable<TEntity> Query() => _dbSet.AsQueryable();

    public Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _dbSet.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        _dbSet.FirstOrDefaultAsync(predicate, cancellationToken);

    public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        _dbSet.AddAsync(entity, cancellationToken).AsTask();

    public void Update(TEntity entity) => _dbSet.Update(entity);
    public void Remove(TEntity entity) => _dbSet.Remove(entity);
}
