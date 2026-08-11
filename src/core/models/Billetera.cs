using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace InterfazMaze.Models;

[Table("billeteras")]
public partial class Billetera : BaseModel
{
    [PrimaryKey("jugador_id", false)]
    public string JugadorId { get; set; }

    [Column("saldo")]
    public decimal Saldo { get; set; }
}
