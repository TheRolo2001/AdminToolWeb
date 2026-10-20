// ============================================================
// Services/InventarioService.cs
// ============================================================
using System.Collections.Concurrent;
using System.Text;
using AdminToolWeb.Models;

namespace AdminToolWeb.Services;

public class InventarioService
{
    public List<EquipoInfo> Resultados   { get; private set; } = [];
    public List<LogEntry>   Log          { get; private set; } = [];
    public bool             Escaneando   { get; private set; }
    public int              Progreso     { get; private set; }
    public int              Total        { get; private set; }
    public event Action?    OnCambio;

    public async Task EscanearAsync(string redBase, int inicio, int fin,
                                    Credenciales? cred, CancellationToken ct)
    {
        Escaneando = true;
        Resultados = [];
        Log        = [];
        Progreso   = 0;

        var ips   = Enumerable.Range(inicio, fin - inicio + 1)
                              .Select(i => $"{redBase}.{i}").ToList();
        Total = ips.Count;

        var bag   = new ConcurrentBag<EquipoInfo>();
        var opts  = new ParallelOptions
        {
            MaxDegreeOfParallelism = RedService.MaxWorkers,
            CancellationToken      = ct,
        };

        await Task.Run(() =>
        {
            Parallel.ForEach(ips, opts, ip =>
            {
                if (ct.IsCancellationRequested) return;
                Interlocked.Increment(ref _progreso);
                Progreso = _progreso;

                if (!RedService.Ping(ip)) { OnCambio?.Invoke(); return; }

                string hostname = RedService.ObtenerHostname(ip);
                var servicios   = RedService.DetectarServicios(ip);
                string tipo     = RedService.DeducirTipo(servicios);

                var equipo = new EquipoInfo
                {
                    IP        = ip,
                    Hostname  = hostname,
                    Servicios = string.Join(", ", servicios),
                    Tipo      = tipo,
                };

                if (cred != null)
                {
                    var hw = WmiService.ObtenerInfo(ip, cred);
                    if (hw != null)
                    {
                        equipo.Serial     = hw.Serial;
                        equipo.Fabricante = hw.Fabricante;
                        equipo.Modelo     = hw.Modelo;
                        equipo.SO         = hw.SO;
                        equipo.VersionSO  = hw.VersionSO;
                        equipo.RamGB      = hw.RamGB;
                        equipo.Dominio    = hw.Dominio;
                        equipo.Usuario    = hw.Usuario;
                        equipo.EstadoWMI  = "OK";
                        Agregar(new LogEntry { Nivel = "OK", Mensaje = $"{ip} — {hostname} — Serial: {hw.Serial}" });
                    }
                    else
                    {
                        equipo.EstadoWMI = "Sin acceso";
                        Agregar(new LogEntry { Nivel = "WARN", Mensaje = $"{ip} — {hostname} — Sin acceso WMI" });
                    }
                }
                else
                {
                    Agregar(new LogEntry { Nivel = "OK", Mensaje = $"{ip} — {hostname}" });
                }

                bag.Add(equipo);
                OnCambio?.Invoke();
            });
        }, ct);

        Resultados = bag.OrderBy(e =>
        {
            var p = e.IP.Split('.');
            return p.Length == 4 && int.TryParse(p[3], out int n) ? n : 0;
        }).ToList();

        Escaneando = false;
        Agregar(new LogEntry { Nivel = "OK", Mensaje = $"Escaneo completo — {Resultados.Count} equipos encontrados." });
        OnCambio?.Invoke();
    }

    private int _progreso;

    private void Agregar(LogEntry entry)
    {
        lock (Log) Log.Insert(0, entry);
        OnCambio?.Invoke();
    }

    // ── Exportar CSV ─────────────────────────────────────────
    public string GenerarCSV()
    {
        var sb = new StringBuilder();
        sb.AppendLine("ip;hostname;serial;fabricante;modelo;so;version_so;ram_gb;dominio;usuario;tipo;servicios;fecha");
        foreach (var e in Resultados)
            sb.AppendLine($"{e.IP};{e.Hostname};{e.Serial};{e.Fabricante};{e.Modelo};" +
                          $"{e.SO};{e.VersionSO};{e.RamGB};{e.Dominio};{e.Usuario};" +
                          $"{e.Tipo};{e.Servicios};{e.FechaEscaneo}");
        return sb.ToString();
    }
}
