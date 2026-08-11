using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace InterfazMaze.Models;

[Table("perfiles")]
public partial class Perfil : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; }

    [Column("username")]
    public string Username { get; set; }

    [Column("oro")]
    public int Oro { get; set; }

    [Column("experiencia")]
    public int Experiencia { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
