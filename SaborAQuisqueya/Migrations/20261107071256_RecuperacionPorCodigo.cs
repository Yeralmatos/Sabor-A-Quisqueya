using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaborAQuisqueya.Migrations
{
    /// <inheritdoc />
    public partial class RecuperacionPorCodigo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecuperacionesContrasena",
                columns: table => new
                {
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    CodigoHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ExpiraUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProximoEnvioUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IntentosFallidos = table.Column<int>(type: "int", nullable: false),
                    BloqueadoHastaUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecuperacionesContrasena", x => x.UsuarioId);
                    table.ForeignKey(
                        name: "FK_RecuperacionesContrasena_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecuperacionesContrasena");
        }
    }
}
