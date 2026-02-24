using Microsoft.EntityFrameworkCore;
using StudentsTests.Models;

namespace StudentsTests.Infrastructure
{
    public class AppDbContext:DbContext
    {
        public DbSet<Students> Students { get; set; } = null!;
        public DbSet<Questions> Questions { get; set; } = null!;
        public DbSet<AnswerOption> AnswerOption { get; set; } = null!;
        public DbSet<StudentAnswer> StudentAnswer { get; set; } = null!;
        public DbSet<Subject> TesSubjectts { get; set; } = null!;
        public DbSet<TestAttempt> TestAttempt { get; set; } = null!;

        public AppDbContext(DbContextOptions<AppDbContext> options):base (options)
        {

        }
    }
}
