using Coding.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coding.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260824183000_AddStripeSubscriptions")]
public sealed class AddStripeSubscriptions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "StripeCustomerId", table: "Users", type: "character varying(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(name: "StripeSubscriptionId", table: "Users", type: "character varying(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(name: "SubscriptionPlan", table: "Users", type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Free");
        migrationBuilder.AddColumn<string>(name: "SubscriptionStatus", table: "Users", type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "inactive");
        migrationBuilder.CreateIndex(name: "IX_Users_StripeCustomerId", table: "Users", column: "StripeCustomerId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_Users_StripeSubscriptionId", table: "Users", column: "StripeSubscriptionId", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Users_StripeCustomerId", table: "Users");
        migrationBuilder.DropIndex(name: "IX_Users_StripeSubscriptionId", table: "Users");
        migrationBuilder.DropColumn(name: "StripeCustomerId", table: "Users");
        migrationBuilder.DropColumn(name: "StripeSubscriptionId", table: "Users");
        migrationBuilder.DropColumn(name: "SubscriptionPlan", table: "Users");
        migrationBuilder.DropColumn(name: "SubscriptionStatus", table: "Users");
    }
}
