using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TaskManager.Models;
using TaskManager.Services;

namespace TaskManager.Data
{
    public class ApplicationDbContext : DbContext, IDataProtectionKeyContext
    {
        private readonly ITenantService _tenantService;

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            ITenantService tenantService)
            : base(options)
        {
            _tenantService = tenantService;
        }

        public DbSet<Organization> Organizations { get; set; } = null!;
        public DbSet<OrganizationMember> OrganizationMembers { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Project> Projects { get; set; } = null!;
        public DbSet<ProjectUser> ProjectUsers { get; set; } = null!;
        public DbSet<TaskItem> Tasks { get; set; } = null!;
        public DbSet<TaskHistory> TaskHistories { get; set; } = null!;
        public DbSet<Subtask> Subtasks { get; set; } = null!;
        public DbSet<Comment> Comments { get; set; } = null!;
        public DbSet<Attachment> Attachments { get; set; } = null!;
        public DbSet<Tag> Tags { get; set; } = null!;
        public DbSet<TaskTag> TaskTags { get; set; } = null!;
        public DbSet<TaskWatcher> TaskWatchers { get; set; } = null!;
        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;

        // ── Billing ─────────────────────────────────────────────────────────
        public DbSet<Plan> Plans { get; set; } = null!;
        public DbSet<PlanFeature> PlanFeatures { get; set; } = null!;
        public DbSet<Subscription> Subscriptions { get; set; } = null!;
        public DbSet<Invoice> Invoices { get; set; } = null!;
        public DbSet<UsageCounter> UsageCounters { get; set; } = null!;
        public DbSet<BillingEvent> BillingEvents { get; set; } = null!;
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; } = null!;
        public DbSet<OrganizationInvite> OrganizationInvites { get; set; } = null!;

        /// <summary>
        /// The organization id used by the global query filters for the current request.
        /// </summary>
        public int? CurrentTenantId => _tenantService.OrganizationId;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── Organization ────────────────────────────────────────────────
            modelBuilder.Entity<Organization>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Slug).IsUnique();

                entity.HasMany(e => e.Users)
                    .WithOne(u => u.Organization)
                    .HasForeignKey(u => u.OrganizationId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(e => e.Projects)
                    .WithOne(p => p.Organization)
                    .HasForeignKey(p => p.OrganizationId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ── OrganizationMember ─────────────────────────────────────────
            modelBuilder.Entity<OrganizationMember>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.OrganizationId, e.UserId }).IsUnique();

                entity.HasOne(e => e.Organization)
                    .WithMany(o => o.Members)
                    .HasForeignKey(e => e.OrganizationId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.OrganizationMemberships)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ── User ────────────────────────────────────────────────────────
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.OrganizationId);

                // Self-referencing foreign key for CreatedBy
                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(e => e.CreatedBy)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Project ─────────────────────────────────────────────────────
            modelBuilder.Entity<Project>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.OrganizationId);

                entity.HasOne(e => e.Manager)
                    .WithMany(u => u.ManagedProjects)
                    .HasForeignKey(e => e.ManagerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ── ProjectUser ─────────────────────────────────────────────────
            modelBuilder.Entity<ProjectUser>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.ProjectId, e.UserId }).IsUnique();

                entity.HasOne(e => e.Project)
                    .WithMany(p => p.ProjectUsers)
                    .HasForeignKey(e => e.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.ProjectUsers)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ── TaskItem ────────────────────────────────────────────────────
            modelBuilder.Entity<TaskItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.AssignedToId);
                entity.HasIndex(e => e.ProjectId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.OrganizationId);

                entity.HasOne(e => e.Project)
                    .WithMany(p => p.Tasks)
                    .HasForeignKey(e => e.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.AssignedTo)
                    .WithMany(u => u.AssignedTasks)
                    .HasForeignKey(e => e.AssignedToId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.AssignedBy)
                    .WithMany(u => u.CreatedTasks)
                    .HasForeignKey(e => e.AssignedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ── TaskHistory ─────────────────────────────────────────────────
            modelBuilder.Entity<TaskHistory>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Task)
                    .WithMany(t => t.History)
                    .HasForeignKey(e => e.TaskId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.ChangedBy)
                    .WithMany()
                    .HasForeignKey(e => e.ChangedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Subtask ─────────────────────────────────────────────────────
            modelBuilder.Entity<Subtask>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TaskId);

                entity.HasOne(e => e.Task)
                    .WithMany(t => t.Subtasks)
                    .HasForeignKey(e => e.TaskId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.AssignedTo)
                    .WithMany()
                    .HasForeignKey(e => e.AssignedToId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Comment ────────────────────────────────────────────────────
            modelBuilder.Entity<Comment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TaskId);

                entity.Property(e => e.Body).IsRequired();

                entity.HasOne(e => e.Task)
                    .WithMany(t => t.Comments)
                    .HasForeignKey(e => e.TaskId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Author)
                    .WithMany()
                    .HasForeignKey(e => e.AuthorId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Self-reference for threaded replies
                entity.HasOne(e => e.ParentComment)
                    .WithMany(c => c.Replies)
                    .HasForeignKey(e => e.ParentCommentId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Attachment ─────────────────────────────────────────────────
            modelBuilder.Entity<Attachment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TaskId);

                entity.HasOne(e => e.Task)
                    .WithMany(t => t.Attachments)
                    .HasForeignKey(e => e.TaskId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.UploadedBy)
                    .WithMany()
                    .HasForeignKey(e => e.UploadedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Tag ─────────────────────────────────────────────────────────
            modelBuilder.Entity<Tag>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.OrganizationId, e.Name }).IsUnique();

                entity.HasOne(e => e.Organization)
                    .WithMany()
                    .HasForeignKey(e => e.OrganizationId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ── TaskTag (many-to-many join) ────────────────────────────────
            modelBuilder.Entity<TaskTag>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.TaskId, e.TagId }).IsUnique();

                entity.HasOne(e => e.Task)
                    .WithMany(t => t.TaskTags)
                    .HasForeignKey(e => e.TaskId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Tag)
                    .WithMany(t => t.TaskTags)
                    .HasForeignKey(e => e.TagId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ── TaskWatcher ────────────────────────────────────────────────
            modelBuilder.Entity<TaskWatcher>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.TaskId, e.UserId }).IsUnique();

                entity.HasOne(e => e.Task)
                    .WithMany(t => t.Watchers)
                    .HasForeignKey(e => e.TaskId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ── RefreshToken ────────────────────────────────────────────────
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Token).IsUnique();
                entity.HasIndex(e => e.UserId);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.RefreshTokens)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ── Plan / PlanFeature (global, not tenant-scoped) ──────────────
            modelBuilder.Entity<Plan>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Code).IsUnique();

                entity.HasMany(e => e.Features)
                    .WithOne(f => f.Plan)
                    .HasForeignKey(f => f.PlanId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<PlanFeature>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.PlanId, e.Key }).IsUnique();
            });

            // ── Subscription (one per organization) ─────────────────────────
            modelBuilder.Entity<Subscription>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.OrganizationId).IsUnique();
                entity.HasIndex(e => e.ProviderSubscriptionId);

                entity.HasOne(e => e.Organization)
                    .WithMany()
                    .HasForeignKey(e => e.OrganizationId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Plan)
                    .WithMany()
                    .HasForeignKey(e => e.PlanId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Invoice ─────────────────────────────────────────────────────
            modelBuilder.Entity<Invoice>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.OrganizationId);
                entity.HasIndex(e => e.ProviderInvoiceId);

                entity.HasOne(e => e.Organization)
                    .WithMany()
                    .HasForeignKey(e => e.OrganizationId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Subscription)
                    .WithMany()
                    .HasForeignKey(e => e.SubscriptionId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ── UsageCounter ────────────────────────────────────────────────
            modelBuilder.Entity<UsageCounter>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.OrganizationId, e.Key, e.Period }).IsUnique();

                entity.HasOne(e => e.Organization)
                    .WithMany()
                    .HasForeignKey(e => e.OrganizationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ── BillingEvent (webhook idempotency, global) ──────────────────
            modelBuilder.Entity<BillingEvent>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.Provider, e.EventId }).IsUnique();
            });

            modelBuilder.Entity<PasswordResetToken>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TokenHash).IsUnique();
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<OrganizationInvite>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TokenHash).IsUnique();
                entity.HasIndex(e => new { e.OrganizationId, e.Email });

                entity.HasOne(e => e.Organization)
                    .WithMany()
                    .HasForeignKey(e => e.OrganizationId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.InvitedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.InvitedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Global tenant query filters ─────────────────────────────────
            // Every tenant-scoped entity is automatically narrowed to the current
            // request's organization. SuperAdmin (OrganizationId == null) sees all
            // rows by bypassing the filter.
            modelBuilder.Entity<User>().HasQueryFilter(e =>
                CurrentTenantId == null || e.OrganizationId == CurrentTenantId);

            modelBuilder.Entity<RefreshToken>().HasQueryFilter(e =>
                CurrentTenantId == null || e.User.OrganizationId == CurrentTenantId);

            modelBuilder.Entity<PasswordResetToken>().HasQueryFilter(e =>
                CurrentTenantId == null || e.User.OrganizationId == CurrentTenantId);

            modelBuilder.Entity<Project>().HasQueryFilter(e =>
                CurrentTenantId == null || e.OrganizationId == CurrentTenantId);

            modelBuilder.Entity<ProjectUser>().HasQueryFilter(e =>
                CurrentTenantId == null || e.Project.OrganizationId == CurrentTenantId);

            modelBuilder.Entity<TaskItem>().HasQueryFilter(e =>
                CurrentTenantId == null || e.OrganizationId == CurrentTenantId);

            modelBuilder.Entity<TaskHistory>().HasQueryFilter(e =>
                CurrentTenantId == null || e.Task.OrganizationId == CurrentTenantId);

            modelBuilder.Entity<OrganizationMember>().HasQueryFilter(e =>
                CurrentTenantId == null || e.OrganizationId == CurrentTenantId);

            // Tenant scoping for the rich-task entities: filter by the parent task's org.
            modelBuilder.Entity<Subtask>().HasQueryFilter(e =>
                CurrentTenantId == null || e.Task.OrganizationId == CurrentTenantId);

            modelBuilder.Entity<Comment>().HasQueryFilter(e =>
                CurrentTenantId == null || e.Task.OrganizationId == CurrentTenantId);

            modelBuilder.Entity<Attachment>().HasQueryFilter(e =>
                CurrentTenantId == null || e.Task.OrganizationId == CurrentTenantId);

            modelBuilder.Entity<TaskTag>().HasQueryFilter(e =>
                CurrentTenantId == null || e.Task.OrganizationId == CurrentTenantId);

            modelBuilder.Entity<TaskWatcher>().HasQueryFilter(e =>
                CurrentTenantId == null || e.Task.OrganizationId == CurrentTenantId);

            // Tags are directly scoped by OrganizationId.
            modelBuilder.Entity<Tag>().HasQueryFilter(e =>
                CurrentTenantId == null || e.OrganizationId == CurrentTenantId);

            // Billing entities scoped directly by OrganizationId.
            modelBuilder.Entity<Subscription>().HasQueryFilter(e =>
                CurrentTenantId == null || e.OrganizationId == CurrentTenantId);

            modelBuilder.Entity<Invoice>().HasQueryFilter(e =>
                CurrentTenantId == null || e.OrganizationId == CurrentTenantId);

            modelBuilder.Entity<UsageCounter>().HasQueryFilter(e =>
                CurrentTenantId == null || e.OrganizationId == CurrentTenantId);

            modelBuilder.Entity<OrganizationInvite>().HasQueryFilter(e =>
                CurrentTenantId == null || e.OrganizationId == CurrentTenantId);
        }
    }
}
