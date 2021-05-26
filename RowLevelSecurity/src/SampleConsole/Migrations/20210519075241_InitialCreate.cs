using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace SampleConsole.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "conditions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    location = table.Column<string>(type: "text", nullable: true),
                    temperature = table.Column<double>(type: "double precision", nullable: true),
                    humidity = table.Column<double>(type: "double precision", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conditions", x => x.id);
                });
            migrationBuilder.Sql("ALTER TABLE conditions ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("CREATE POLICY conditions_isolation_policy ON conditions FOR ALL USING (tenant_id = current_setting('app.current_tenant')::BIGINT);");
            migrationBuilder.Sql("ALTER TABLE conditions FORCE ROW LEVEL SECURITY;");

            migrationBuilder.CreateIndex(
                name: "conditions_tenant_id_location_idx",
                table: "conditions",
                columns: new[] { "tenant_id", "location" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "conditions");
        }
    }
}
