using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using Supabase;
using InterfazMaze.Models;

public partial class SupabaseManager : Node
{
    public static SupabaseManager Instance { get; private set; }

    public Supabase.Client Client { get; private set; }
    public Jugador CurrentJugador { get; private set; }
    public Perfil CurrentPerfil { get; private set; }
    public Billetera CurrentBilletera { get; private set; }

    public bool IsInitialized { get; private set; } = false;

    public override void _Ready()
    {
        if (Instance == null)
        {
            Instance = this;
            _ = InitializeSupabaseAsync();
        }
        else
        {
            QueueFree();
        }
    }

    public async Task InitializeSupabaseAsync()
    {
        if (IsInitialized) return;

        try
        {
            var url = InterfazMaze.SupabaseConfig.SupabaseUrl;
            var key = InterfazMaze.SupabaseConfig.SupabaseAnonKey;

            var options = new SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = true
            };

            Client = new Supabase.Client(url, key, options);
            await Client.InitializeAsync();
            IsInitialized = true;
            GD.Print("Supabase inicializado correctamente en Maze Survivor.");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error al inicializar Supabase: {ex.Message}");
        }
    }

    public decimal GetSaldo()
    {
        if (CurrentBilletera != null) return CurrentBilletera.Saldo;
        if (CurrentJugador != null) return CurrentJugador.Saldo;
        return 100.00m;
    }

    /// <summary>
    /// Registra un nuevo usuario en Supabase Auth y crea sus registros en 'jugadores', 'perfiles' y 'billeteras'.
    /// </summary>
    public async Task<(bool success, string error)> SignUpAsync(string email, string password, string nombre, string username, string rol = "jugador")
    {
        try
        {
            if (Client == null || !IsInitialized)
            {
                await InitializeSupabaseAsync();
            }

            var session = await Client.Auth.SignUp(email, password);
            if (session?.User == null)
            {
                return (false, "No se pudo crear el usuario en Supabase Auth.");
            }

            string userId = session.User.Id;
            decimal initialSaldo = 100.00m;

            var nuevoJugador = new Jugador
            {
                Id = userId,
                Nombre = nombre,
                Rol = string.IsNullOrEmpty(rol) ? "jugador" : rol.ToLower(),
                Saldo = initialSaldo,
                Estado = true,
                CreatedAt = DateTime.UtcNow
            };
            await Client.From<Jugador>().Insert(nuevoJugador);

            var nuevoPerfil = new Perfil
            {
                Id = userId,
                Username = username,
                Oro = 100,
                Experiencia = 0,
                UpdatedAt = DateTime.UtcNow
            };
            await Client.From<Perfil>().Insert(nuevoPerfil);

            var nuevaBilletera = new Billetera
            {
                JugadorId = userId,
                Saldo = initialSaldo
            };
            await Client.From<Billetera>().Insert(nuevaBilletera);

            CurrentJugador = nuevoJugador;
            CurrentPerfil = nuevoPerfil;
            CurrentBilletera = nuevaBilletera;

            return (true, null);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error en SignUpAsync: {ex}");
            string msg = ex.Message;
            if (msg.Contains("22P02"))
            {
                return (false, "Error de tipo ENUM en Supabase (22P02). Ejecuta el script SQL en el panel de Supabase para habilitar el valor 'jugador'.");
            }
            if (msg.Contains("42501"))
            {
                return (false, "Error de permisos RLS en Supabase (42501). Ejecuta el script SQL en el panel de Supabase para autorizar inserciones.");
            }
            if (msg.Contains("already registered") || msg.Contains("already exists"))
            {
                return (false, "El correo electrónico ya se encuentra registrado. Intenta iniciar sesión.");
            }
            return (false, msg);
        }
    }

    /// <summary>
    /// Inicia sesión con Email y Password, cargando el perfil y jugador del usuario.
    /// </summary>
    public async Task<(bool success, string error)> SignInAsync(string email, string password)
    {
        try
        {
            if (Client == null || !IsInitialized)
            {
                await InitializeSupabaseAsync();
            }

            var session = await Client.Auth.SignInWithPassword(email, password);
            if (session?.User == null)
            {
                return (false, "Credenciales inválidas.");
            }

            string userId = session.User.Id;
            await CargarDatosUsuarioAsync(userId);

            return (true, null);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error en SignInAsync: {ex}");
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Carga los datos de las tablas 'jugadores', 'perfiles' y 'billeteras' del usuario autenticado.
    /// </summary>
    public async Task CargarDatosUsuarioAsync(string userId)
    {
        try
        {
            var jugadorRes = await Client.From<Jugador>().Where(x => x.Id == userId).Single();
            CurrentJugador = jugadorRes;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Nota al cargar jugador: {ex.Message}");
        }

        try
        {
            var perfilRes = await Client.From<Perfil>().Where(x => x.Id == userId).Single();
            CurrentPerfil = perfilRes;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Nota al cargar perfil: {ex.Message}");
        }

        try
        {
            var billeteraRes = await Client.From<Billetera>().Where(x => x.JugadorId == userId).Single();
            CurrentBilletera = billeteraRes;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Nota al cargar billetera: {ex.Message}");
        }

        if (CurrentBilletera != null && CurrentJugador != null)
        {
            if (CurrentBilletera.Saldo != CurrentJugador.Saldo)
            {
                CurrentBilletera.Saldo = CurrentJugador.Saldo;
            }
        }
    }

    public async Task SignOutAsync()
    {
        try
        {
            if (Client != null)
            {
                await Client.Auth.SignOut();
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error al cerrar sesión: {ex.Message}");
        }
        finally
        {
            CurrentJugador = null;
            CurrentPerfil = null;
            CurrentBilletera = null;
        }
    }

    // --- MÉTODOS DEL SISTEMA DE APUESTAS Y LIQUIDACIÓN ---

    public async Task<System.Collections.Generic.List<Partida>> ObtenerPartidasActivasAsync()
    {
        try
        {
            if (Client == null) return new System.Collections.Generic.List<Partida>();
            var res = await Client.From<Partida>().Get();
            return res.Models;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error al obtener partidas: {ex.Message}");
            return new System.Collections.Generic.List<Partida>();
        }
    }

    public async Task<System.Collections.Generic.List<Jugador>> ObtenerJugadoresDisponiblesAsync()
    {
        try
        {
            if (Client == null) return new System.Collections.Generic.List<Jugador>();
            var res = await Client.From<Jugador>().Get();
            return res.Models;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error al obtener jugadores: {ex.Message}");
            return new System.Collections.Generic.List<Jugador>();
        }
    }

    public async Task<(bool success, string error)> RealizarApuestaAsync(string partidaId, string jugadorPronosticadoId, string tipoMercado, decimal monto, decimal cuota)
    {
        try
        {
            if (monto <= 0)
            {
                return (false, "El monto a apostar debe ser mayor a 0.");
            }

            decimal saldoDisponible = GetSaldo();

            if (saldoDisponible < monto)
            {
                return (false, $"Saldo insuficiente. Tu saldo actual es: ${saldoDisponible:F2}");
            }

            string espectadorId = CurrentJugador != null ? CurrentJugador.Id : Guid.NewGuid().ToString();
            string nuevaApuestaId = Guid.NewGuid().ToString();

            var nuevaApuesta = new Apuesta
            {
                Id = nuevaApuestaId,
                EspectadorId = espectadorId,
                Monto = monto,
                CuotaTotal = cuota,
                GananciaPotencial = monto * cuota,
                Estado = "pendiente",
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await Client.From<Apuesta>().Insert(nuevaApuesta);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Nota al insertar en tabla apuestas: {ex.Message}");
            }

            var nuevoDetalle = new DetalleApuesta
            {
                ApuestaId = nuevaApuestaId,
                PartidaId = partidaId,
                TipoMercado = tipoMercado,
                JugadorPronosticadoId = jugadorPronosticadoId,
                CuotaSeleccion = cuota,
                EstadoSeleccion = "pendiente",
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await Client.From<DetalleApuesta>().Insert(nuevoDetalle);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Nota al insertar detalle_apuesta: {ex.Message}");
            }

            decimal nuevoSaldo = saldoDisponible - monto;

            if (CurrentBilletera != null)
            {
                CurrentBilletera.Saldo = nuevoSaldo;
                try
                {
                    await Client.From<Billetera>().Where(x => x.JugadorId == espectadorId).Update(CurrentBilletera);
                }
                catch { }
            }

            if (CurrentJugador != null)
            {
                CurrentJugador.Saldo = nuevoSaldo;
                try
                {
                    await Client.From<Jugador>().Where(x => x.Id == espectadorId).Update(CurrentJugador);
                }
                catch { }
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error al realizar apuesta: {ex}");
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Liquidación al finalizar la partida: calcula las ganancias de cada espectador y del jugador ganador, 
    /// y transfiere los fondos a sus billeteras y perfiles en Supabase.
    /// </summary>
    public async Task<(bool success, string summary)> LiquidarPartidaAsync(string partidaId, string ganadorId = null, string primeraKillId = null, string primeraLlaveId = null, decimal pozoGanadorJugador = 200.00m)
    {
        try
        {
            if (Client == null || !IsInitialized)
            {
                await InitializeSupabaseAsync();
            }

            GD.Print($"[Liquidador] Iniciando liquidación de partida '{partidaId}'. Ganador: '{ganadorId ?? "Ninguno"}'");
            int apuestasGanadas = 0;
            int apuestasPerdidas = 0;
            decimal totalPagadoEspectadores = 0m;

            // 1. Transferir el pozo acumulado al jugador que logró escapar por la puerta
            if (!string.IsNullOrEmpty(ganadorId))
            {
                try
                {
                    var jWinnerRes = await Client.From<Jugador>().Where(x => x.Id == ganadorId).Get();
                    var jWinner = jWinnerRes.Models.FirstOrDefault();
                    if (jWinner != null)
                    {
                        jWinner.Saldo += pozoGanadorJugador;
                        await Client.From<Jugador>().Where(x => x.Id == ganadorId).Update(jWinner);
                    }

                    var bWinnerRes = await Client.From<Billetera>().Where(x => x.JugadorId == ganadorId).Get();
                    var bWinner = bWinnerRes.Models.FirstOrDefault();
                    if (bWinner != null)
                    {
                        bWinner.Saldo += pozoGanadorJugador;
                        await Client.From<Billetera>().Where(x => x.JugadorId == ganadorId).Update(bWinner);
                    }

                    if (CurrentJugador != null && CurrentJugador.Id == ganadorId)
                    {
                        CurrentJugador.Saldo += pozoGanadorJugador;
                        if (CurrentBilletera != null) CurrentBilletera.Saldo += pozoGanadorJugador;
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[Liquidador] Nota al transferir pozo a jugador ganador: {ex.Message}");
                }
            }

            // 2. Obtener y procesar todos los detalles de apuestas
            var detallesRes = await Client.From<DetalleApuesta>().Get();
            var detallesList = detallesRes.Models.Where(d => string.IsNullOrEmpty(partidaId) || d.PartidaId == partidaId || d.EstadoSeleccion == "pendiente").ToList();

            foreach (var detalle in detallesList)
            {
                bool esGanadora = false;
                string mercado = detalle.TipoMercado?.ToLower() ?? "";

                if (mercado.Contains("ganador") && !string.IsNullOrEmpty(ganadorId) && (detalle.JugadorPronosticadoId == ganadorId || detalle.JugadorPronosticadoId.Contains(ganadorId)))
                {
                    esGanadora = true;
                }
                else if (mercado.Contains("kill") && !string.IsNullOrEmpty(primeraKillId) && detalle.JugadorPronosticadoId == primeraKillId)
                {
                    esGanadora = true;
                }
                else if (mercado.Contains("llave") && !string.IsNullOrEmpty(primeraLlaveId) && detalle.JugadorPronosticadoId == primeraLlaveId)
                {
                    esGanadora = true;
                }

                detalle.EstadoSeleccion = esGanadora ? "ganada" : "perdida";
                try
                {
                    await Client.From<DetalleApuesta>().Where(x => x.Id == detalle.Id).Update(detalle);
                }
                catch { }

                if (!string.IsNullOrEmpty(detalle.ApuestaId))
                {
                    var apuestaRes = await Client.From<Apuesta>().Where(x => x.Id == detalle.ApuestaId).Get();
                    var apuesta = apuestaRes.Models.FirstOrDefault();

                    if (apuesta != null)
                    {
                        apuesta.Estado = esGanadora ? "ganada" : "perdida";
                        try
                        {
                            await Client.From<Apuesta>().Where(x => x.Id == apuesta.Id).Update(apuesta);
                        }
                        catch { }

                        if (esGanadora)
                        {
                            apuestasGanadas++;
                            decimal ganancia = apuesta.GananciaPotencial ?? (apuesta.Monto * apuesta.CuotaTotal);
                            totalPagadoEspectadores += ganancia;

                            string espectadorId = apuesta.EspectadorId;

                            try
                            {
                                var bSpecRes = await Client.From<Billetera>().Where(x => x.JugadorId == espectadorId).Get();
                                var bSpec = bSpecRes.Models.FirstOrDefault();
                                if (bSpec != null)
                                {
                                    bSpec.Saldo += ganancia;
                                    await Client.From<Billetera>().Where(x => x.JugadorId == espectadorId).Update(bSpec);
                                }
                            }
                            catch { }

                            try
                            {
                                var jSpecRes = await Client.From<Jugador>().Where(x => x.Id == espectadorId).Get();
                                var jSpec = jSpecRes.Models.FirstOrDefault();
                                if (jSpec != null)
                                {
                                    jSpec.Saldo += ganancia;
                                    await Client.From<Jugador>().Where(x => x.Id == espectadorId).Update(jSpec);
                                }
                            }
                            catch { }

                            if (CurrentJugador != null && CurrentJugador.Id == espectadorId)
                            {
                                CurrentJugador.Saldo += ganancia;
                                if (CurrentBilletera != null) CurrentBilletera.Saldo += ganancia;
                            }
                        }
                        else
                        {
                            apuestasPerdidas++;
                        }
                    }
                }
            }

            string summary = $"Partida Finalizada. Apuestas pagadas: {apuestasGanadas} (${totalPagadoEspectadores:F2}), Perdidas: {apuestasPerdidas}.";
            GD.Print($"[Liquidador] {summary}");
            return (true, summary);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Liquidador] Error al liquidar partida: {ex.Message}");
            return (false, ex.Message);
        }
    }

    public async Task<System.Collections.Generic.List<Apuesta>> ObtenerMisApuestasAsync()
    {
        try
        {
            if (CurrentJugador == null || Client == null) 
                return new System.Collections.Generic.List<Apuesta>();

            var res = await Client.From<Apuesta>()
                .Where(x => x.EspectadorId == CurrentJugador.Id)
                .Get();

            return res.Models;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error al obtener apuestas del usuario: {ex.Message}");
            return new System.Collections.Generic.List<Apuesta>();
        }
    }
}
