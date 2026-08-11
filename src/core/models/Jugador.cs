using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace InterfazMaze.Models;

[Table("jugadores")]
public partial class Jugador : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; }

    [Column("nombre")]
    public string Nombre { get; set; }

    [Column("rol")]
    public string Rol { get; set; }

    [Column("saldo")]
    public decimal Saldo { get; set; }

    [Column("estado")]
    public bool Estado { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
