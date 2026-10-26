using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Tot.Integrations.Persistence;

#nullable disable
#pragma warning disable CS1591 // Missing XML comment — auto-generated migration file

namespace Tot.Integrations.Persistence.Migrations;

/// <inheritdoc />
[DbContext(typeof(IntegrationsDbContext))]
[Migration("20261019000000_AddLockedAtToCommands")]
internal partial class AddLockedAtToCommands : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // R-05: add locked_at so RecoverStuckAsync can detect rows whose dispatcher instance
        // died mid-execution, rather than using created_at (which never changes after insert).
        migrationBuilder.Sql("""
            ALTER TABLE integrations.commands
                ADD COLUMN IF NOT EXISTS locked_at timestamptz;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE integrations.commands
                DROP COLUMN IF EXISTS locked_at;
            """);
    }
}
