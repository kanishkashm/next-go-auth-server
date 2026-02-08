using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace next_go_api.Migrations
{
    /// <inheritdoc />
    public partial class AddUpgradeRequestsAndPlanEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                schema: "identity",
                table: "SubscriptionPlans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsPopular",
                schema: "identity",
                table: "SubscriptionPlans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyPrice",
                schema: "identity",
                table: "SubscriptionPlans",
                type: "numeric",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UpgradeRequests",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedById = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProcessedById = table.Column<string>(type: "text", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UpgradeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UpgradeRequests_AspNetUsers_ProcessedById",
                        column: x => x.ProcessedById,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UpgradeRequests_AspNetUsers_RequestedById",
                        column: x => x.RequestedById,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UpgradeRequests_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "identity",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UpgradeRequests_SubscriptionPlans_CurrentPlanId",
                        column: x => x.CurrentPlanId,
                        principalSchema: "identity",
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UpgradeRequests_SubscriptionPlans_RequestedPlanId",
                        column: x => x.RequestedPlanId,
                        principalSchema: "identity",
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UpgradeRequests_CurrentPlanId",
                schema: "identity",
                table: "UpgradeRequests",
                column: "CurrentPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_UpgradeRequests_OrganizationId",
                schema: "identity",
                table: "UpgradeRequests",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_UpgradeRequests_ProcessedById",
                schema: "identity",
                table: "UpgradeRequests",
                column: "ProcessedById");

            migrationBuilder.CreateIndex(
                name: "IX_UpgradeRequests_RequestedById",
                schema: "identity",
                table: "UpgradeRequests",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_UpgradeRequests_RequestedPlanId",
                schema: "identity",
                table: "UpgradeRequests",
                column: "RequestedPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_UpgradeRequests_Status",
                schema: "identity",
                table: "UpgradeRequests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UpgradeRequests",
                schema: "identity");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                schema: "identity",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "IsPopular",
                schema: "identity",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "MonthlyPrice",
                schema: "identity",
                table: "SubscriptionPlans");
        }
    }
}
