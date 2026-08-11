using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace InterfazMaze.Models;

[Table("partidas")]
public partial class Partida : BaseModel
{
    [PrimaryKey("id", true)]
    public string Id { get; set; }

    [Column("estado")]
    public string Estado { get; set; }

    [Column("pozo_acumulado")]
    public decimal PozoAcumulado { get; set; }

    [Column("ganador_id")]
    public string GanadorId { get; set; }

    [Column("primera_kill_id")]
    public string PrimeraKillId { get; set; }

    [Column("primera_llave_id")]
    public string PrimeraLlaveId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
