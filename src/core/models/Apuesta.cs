using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace InterfazMaze.Models;

[Table("apuestas")]
public partial class Apuesta : BaseModel
{
    [PrimaryKey("id", true)]
    public string Id { get; set; }

    [Column("espectador_id")]
    public string EspectadorId { get; set; }

    [Column("monto")]
    public decimal Monto { get; set; }

    [Column("cuota_total")]
    public decimal CuotaTotal { get; set; }

    [Column("ganancia_potencial")]
    public decimal? GananciaPotencial { get; set; }

    [Column("estado")]
    public string Estado { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
