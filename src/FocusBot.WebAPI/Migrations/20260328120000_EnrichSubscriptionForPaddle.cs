using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusBot.WebAPI.Migrations;

/// <inheritdoc />
public partial class EnrichSubscriptionForPaddle : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Status",
            table: "Subscriptions",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(20)",
            oldMaxLength: 20);

        migrationBuilder.AlterColumn<string>(
            name: "PaddleCustomerId",
            table: "Subscriptions",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.AddColumn<string>(
            name: "BillingInterval",
            table: "Subscriptions",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CancellationReason",
            table: "Subscriptions",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CardLastFour",
            table: "Subscriptions",
            type: "character varying(10)",
            maxLength: 10,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "CancelledAtUtc",
            table: "Subscriptions",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CurrencyCode",
            table: "Subscriptions",
            type: "character varying(10)",
            maxLength: 10,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CustomerEmail",
            table: "Subscriptions",
            type: "character varying(320)",
            maxLength: 320,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "NextBilledAtUtc",
            table: "Subscriptions",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PaddlePriceId",
            table: "Subscriptions",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PaddleProductId",
            table: "Subscriptions",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PaddleTransactionId",
            table: "Subscriptions",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PaymentMethodType",
            table: "Subscriptions",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "UnitAmountMinor",
            table: "Subscriptions",
            type: "bigint",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "BillingInterval", table: "Subscriptions");
        migrationBuilder.DropColumn(name: "CancellationReason", table: "Subscriptions");
        migrationBuilder.DropColumn(name: "CardLastFour", table: "Subscriptions");
        migrationBuilder.DropColumn(name: "CancelledAtUtc", table: "Subscriptions");
        migrationBuilder.DropColumn(name: "CurrencyCode", table: "Subscriptions");
        migrationBuilder.DropColumn(name: "CustomerEmail", table: "Subscriptions");
        migrationBuilder.DropColumn(name: "NextBilledAtUtc", table: "Subscriptions");
        migrationBuilder.DropColumn(name: "PaddlePriceId", table: "Subscriptions");
        migrationBuilder.DropColumn(name: "PaddleProductId", table: "Subscriptions");
        migrationBuilder.DropColumn(name: "PaddleTransactionId", table: "Subscriptions");
        migrationBuilder.DropColumn(name: "PaymentMethodType", table: "Subscriptions");
        migrationBuilder.DropColumn(name: "UnitAmountMinor", table: "Subscriptions");

        migrationBuilder.AlterColumn<string>(
            name: "PaddleCustomerId",
            table: "Subscriptions",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Status",
            table: "Subscriptions",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(32)",
            oldMaxLength: 32);
    }
}
