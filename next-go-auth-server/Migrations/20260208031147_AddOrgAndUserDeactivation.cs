using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace next_go_api.Migrations
{
    /// <inheritdoc />
    public partial class AddOrgAndUserDeactivation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeactivatedAt",
                schema: "identity",
                table: "Organizations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeactivationReason",
                schema: "identity",
                table: "Organizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "identity",
                table: "Organizations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeactivatedAt",
                schema: "identity",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeactivationReason",
                schema: "identity",
                table: "AspNetUsers",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeactivatedAt",
                schema: "identity",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "DeactivationReason",
                schema: "identity",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "identity",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "DeactivatedAt",
                schema: "identity",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DeactivationReason",
                schema: "identity",
                table: "AspNetUsers");
        }
    }
}
