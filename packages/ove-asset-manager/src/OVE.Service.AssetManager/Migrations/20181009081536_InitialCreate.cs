using System;
using Microsoft.EntityFrameworkCore.Migrations;
// ReSharper disable All

namespace OVE.Service.AssetManager.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OVEAssetModels",
                columns: table => new
                {
                    Id = table.Column<string>(nullable: false),
                    Project = table.Column<string>(maxLength: 63, nullable: false),
                    Name = table.Column<string>(maxLength: 50, nullable: false),
                    Description = table.Column<string>(nullable: true),
                    LastModified = table.Column<DateTime>(nullable: false),
                    Service = table.Column<string>(nullable: false),
                    StorageLocation = table.Column<string>(nullable: true),
                    ProcessingErrors = table.Column<string>(nullable: true),
                    ProcessingState = table.Column<int>(nullable: false),
                    AssetMeta = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OVEAssetModels", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OVEAssetModels");
        }
    }
}
