using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>EF Core context. Fluent configuration mirrors the Part 1 ERD and SQL script.</summary>
public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<EventType> EventTypes => Set<EventType>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<EventEnrolment> EventEnrolments => Set<EventEnrolment>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users", t => t.HasCheckConstraint("CK_Users_Role", "[Role] IN ('Organiser', 'Participant')"));
            e.HasIndex(u => u.Email).IsUnique().HasDatabaseName("UQ_Users_Email");
            e.Property(u => u.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<EventType>(e =>
        {
            e.HasIndex(t => t.TypeName).IsUnique().HasDatabaseName("UQ_EventTypes_TypeName");
            e.HasData(
                new EventType { EventTypeId = 1, TypeName = "Run" },
                new EventType { EventTypeId = 2, TypeName = "Walk" },
                new EventType { EventTypeId = 3, TypeName = "Cycle" });
        });

        modelBuilder.Entity<Event>(e =>
        {
            e.Property(x => x.DistanceKm).HasPrecision(6, 2);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasOne(x => x.Organiser).WithMany(u => u.OrganisedEvents)
                .HasForeignKey(x => x.OrganiserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.EventType).WithMany(t => t.Events)
                .HasForeignKey(x => x.EventTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.Property(x => x.DistanceKm).HasPrecision(6, 2);
            e.HasOne(x => x.Event).WithMany(ev => ev.Categories)
                .HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EventEnrolment>(e =>
        {
            e.ToTable("EventEnrolments", t => t.HasCheckConstraint(
                "CK_Enrolments_Status", "[EnrolmentStatus] IN ('Pending', 'Confirmed', 'Cancelled')"));
            e.Property(x => x.EnrolmentStatus).HasDefaultValue(EnrolmentStatuses.Pending);
            e.Property(x => x.EnrolmentDate).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasIndex(x => new { x.ParticipantId, x.EventId }).IsUnique()
                .HasDatabaseName("UQ_Enrolments_ParticipantEvent");

            e.HasOne(x => x.Participant).WithMany(u => u.Enrolments)
                .HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Event).WithMany(ev => ev.Enrolments)
                .HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category).WithMany(c => c.Enrolments)
                .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Result>(e =>
        {
            e.HasOne(r => r.Enrolment).WithOne(en => en.Result)
                .HasForeignKey<Result>(r => r.EnrolmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => r.EnrolmentId).IsUnique().HasDatabaseName("UQ_Results_Enrolment");
        });
    }
}
