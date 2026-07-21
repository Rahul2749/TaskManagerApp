using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManager.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationOnboardingSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BrandPrimaryColor",
                table: "Organizations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OnboardingCompletedAt",
                table: "Organizations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "Organizations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Asia/Kolkata");

            // Existing workspaces skip the new wizard; only fresh signups need it.
            migrationBuilder.Sql(
                """
                UPDATE "Organizations"
                SET "OnboardingCompletedAt" = COALESCE("CreatedAt", NOW() AT TIME ZONE 'utc')
                WHERE "OnboardingCompletedAt" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BrandPrimaryColor",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "OnboardingCompletedAt",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "Organizations");
        }
    }
}
