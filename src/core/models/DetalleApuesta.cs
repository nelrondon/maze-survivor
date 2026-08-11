using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace InterfazMaze.Models;

[Table("detalles_apuestas")]
public partial class DetalleApuesta : BaseModel
{
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    [Column("apuesta_id")]
    public string ApuestaId { get; set; }

    [Column("partida_id")]
    public string PartidaId { get; set; }

    [Column("tipo_mercado")]
    public string TipoMercado { get; set; }

    [Column("jugador_pronosticado_id")]
    public string JugadorPronosticadoId { get; set; }

    [Column("cuota_seleccion")]
    public decimal CuotaSeleccion { get; set; }

    [Column("estado_seleccion")]
    public string EstadoSeleccion { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
