using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieShop.Data;

namespace MovieShop.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public abstract class BaseController<T> : ControllerBase where T : class
    {
        protected readonly AppDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public BaseController(AppDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        // GET: api/[controller]
        [HttpGet]
        public virtual async Task<ActionResult<IEnumerable<T>>> GetAll()
        {
            return await _dbSet.ToListAsync();
        }

        // GET: api/[controller]/5
        [HttpGet("{id}")]
        public virtual async Task<ActionResult<T>> GetById(int id)
        {
            var entity = await _dbSet.FindAsync(id);
            if (entity == null) return NotFound();
            return entity;
        }

        // POST: api/[controller]
        [HttpPost]
        public virtual async Task<ActionResult<T>> Create(T entity)
        {
            _dbSet.Add(entity);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = _context.Entry(entity).Property("Id").CurrentValue }, entity);
        }

        // PUT: api/[controller]/5
        [HttpPut("{id}")]
        public virtual async Task<IActionResult> Update(int id, T entity)
        {
            var entityId = (int)_context.Entry(entity).Property("Id").CurrentValue!;
            if (id != entityId) return BadRequest();

            _context.Entry(entity).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _dbSet.AnyAsync(e => EF.Property<int>(e, "Id") == id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // DELETE: api/[controller]/5
        [HttpDelete("{id}")]
        public virtual async Task<IActionResult> Delete(int id)
        {
            var entity = await _dbSet.FindAsync(id);
            if (entity == null) return NotFound();

            _dbSet.Remove(entity);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
