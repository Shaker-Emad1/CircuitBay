using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CircuitBay.Infrastructure.Migrations;

[DbContext(typeof(Persistence.CircuitBayDbContext))]
[Migration("20260521030000_AddInventoryRowVersion")]
public partial class AddInventoryRowVersion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte[]>(
            name: "RowVersion",
            table: "inventory_items",
            type: "rowversion",
            rowVersion: true,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RowVersion",
            table: "inventory_items");
    }
}
