// ============================================================
// Services/RenombrarService.cs
// ============================================================
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using AdminToolWeb.Models;

namespace AdminToolWeb.Services;

public class RenombrarService
{
    public List<ItemRenombrado> Cola      { get; private set; } = [];
    public List<LogEntry>       Log       { get; private set; } = [];
    public bool                 Ejecutando { get; private set; }
    public event Action?        OnCambio;

    // ── Validación ────────────────────────────────────────────
    public static (bool Valido, string Motivo) ValidarNombre(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return (false, "Nombre vacío");
        if (nombre.Length > 15)               return (false, $"Máx 15 caracteres ({nombre.Length})");
        if (!Regex.IsMatch(nombre, @"^[A-Za-z0-9\-]+$")) return (false, "Solo letras, números y guiones");
        if (nombre.StartsWith('-') || nombre.EndsWith('-')) return (false, "No puede empezar/terminar con guión");
        return (true, "");
    }

    public static string GenerarConPatron(string patron, int numero) =>
        patron.Replace("{N4}", numero.ToString().PadLeft(4, '0'))
              .Replace("{N3}", numero.ToString().PadLeft(3, '0'))
              .Replace("{N2}", numero.ToString().PadLeft(2, '0'))
              .Replace("{N}",  numero.ToString());

    // ── Cola ─────────────────────────────────────────────────
    public void AgregarItem(string ip, string nombreActual, string nuevoNombre)
    {
        var (v, m) = ValidarNombre(nuevoNombre);
        Cola.Add(new ItemRenombrado
        {
            IP           = ip,
            NombreActual = nombreActual,
            NuevoNombre  = nuevoNombre,
            Resultado    = v ? "Pendiente" : $"Inválido: {m}",
        });
        OnCambio?.Invoke();
    }

    public void EliminarItem(int index)
    {
        if (index >= 0 && index < Cola.Count) Cola.RemoveAt(index);
        OnCambio?.Invoke();
    }

    public void LimpiarCola() { Cola.Clear(); OnCambio?.Invoke(); }

    // ── Escaneo para poblar cola ──────────────────────────────
    public async Task<List<(string ip, string nombre, string canal)>> EscanearEquiposAsync(
        string redBase, Credenciales cred, IProgress<string>? progreso = null)
    {
        var resultado = new System.Collections.Concurrent.ConcurrentBag<(string, string, string)>();
        var ips = Enumerable.Range(1, 254).Select(i => $"{redBase}.{i}").ToList();

        await Task.Run(() =>
        {
            Parallel.ForEach(ips, new ParallelOptions { MaxDegreeOfParallelism = 30 }, ip =>
            {
                if (!RedService.Ping(ip)) return;
                bool winrm = RedService.WinRmDisponible(ip);
                bool smb   = RedService.SmbDisponible(ip);
                if (!winrm && !smb) return;
                string? nombre = ObtenerNombreRemoto(ip, cred);
                if (nombre == null) return;
                string canal = winrm ? "WinRM" : "SMB/WMI";
                resultado.Add((ip, nombre, canal));
                progreso?.Report($"{ip} — {nombre} ({canal})");
            });
        });

        return resultado.OrderBy(t =>
        {
            var p = t.Item1.Split('.');
            return p.Length == 4 && int.TryParse(p[3], out int n) ? n : 0;
        }).ToList();
    }

    // ── Obtener nombre remoto ─────────────────────────────────
    public string? ObtenerNombreRemoto(string ip, Credenciales cred)
    {
        string? nombre = WmiService.ObtenerNombre(ip, cred);
        if (nombre != null) return nombre;

        if (RedService.WinRmDisponible(ip))
        {
            string r = EjecutarPS(
                $"$s=ConvertTo-SecureString '{EscPS(cred.Password)}' -AsPlainText -Force;" +
                $"$c=New-Object PSCredential('{EscPS(cred.UsuarioCompleto)}',$s);" +
                $"Invoke-Command -ComputerName {ip} -Credential $c -ScriptBlock{{$env:COMPUTERNAME}}");
            if (!string.IsNullOrWhiteSpace(r)) return r.Trim();
        }

        return ObtenerNombreWmic(ip, cred);
    }

    private string? ObtenerNombreWmic(string ip, Credenciales cred)
    {
        try
        {
            var psi = new ProcessStartInfo("wmic",
                $"/node:{ip} /user:{cred.UsuarioCompleto} /password:{cred.Password} computersystem get name")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = Process.Start(psi);
            var lines = (p?.StandardOutput.ReadToEnd() ?? "").Split('\n')
                .Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            p?.WaitForExit(15000);
            return lines.Count > 1 ? lines[1] : null;
        }
        catch { return null; }
    }

    // ── Ejecutar cola ─────────────────────────────────────────
    public async Task EjecutarColaAsync(Credenciales cred, bool reiniciar)
    {
        Ejecutando = true;
        Log.Clear();
        OnCambio?.Invoke();

        var pendientes = Cola.Where(i => i.Resultado == "Pendiente").ToList();

        await Task.Run(() =>
        {
            foreach (var item in pendientes)
            {
                item.Resultado = "Procesando...";
                OnCambio?.Invoke();

                var (exito, metodo) = RenombrarRemoto(item.IP, item.NuevoNombre, cred, reiniciar);
                item.Resultado = exito ? "OK" : "ERROR";
                item.Metodo    = metodo;

                Log.Insert(0, new LogEntry
                {
                    Nivel   = exito ? "OK" : "ERROR",
                    Mensaje = $"{item.IP} → {item.NuevoNombre} [{metodo}]"
                });
                OnCambio?.Invoke();
            }
        });

        GuardarLog(pendientes);
        Ejecutando = false;
        OnCambio?.Invoke();
    }

    // ── Métodos de renombrado en cascada ─────────────────────
    private (bool, string) RenombrarRemoto(string ip, string nombre, Credenciales cred, bool reiniciar)
    {
        if (RedService.SmbDisponible(ip) && WmiService.Renombrar(ip, nombre, cred, reiniciar))
            return (true, "WMI.NET");
        if (RedService.WinRmDisponible(ip) && RenombrarWinRM(ip, nombre, cred, reiniciar))
            return (true, "WinRM/PS");
        if (RenombrarWmic(ip, nombre, cred, reiniciar))
            return (true, "wmic.exe");
        return (false, "todos fallaron");
    }

    private bool RenombrarWinRM(string ip, string nombre, Credenciales cred, bool reiniciar)
    {
        string flag = reiniciar ? "-Restart" : "";
        string u    = EscPS(cred.UsuarioCompleto);
        string pw   = EscPS(cred.Password);
        string cmd  =
            $"Invoke-Command -ComputerName {ip} " +
            $"-Credential (New-Object PSCredential('{u}',(ConvertTo-SecureString '{pw}' -AsPlainText -Force))) " +
            $"-ScriptBlock {{Rename-Computer -NewName '{EscPS(nombre)}' " +
            $"-DomainCredential (New-Object PSCredential('{u}',(ConvertTo-SecureString '{pw}' -AsPlainText -Force))) -Force {flag}}}";
        try
        {
            var psi = new ProcessStartInfo("powershell", $"-Command \"{cmd}\"")
            { RedirectStandardOutput = true, RedirectStandardError = true,
              UseShellExecute = false, CreateNoWindow = true };
            using var p = Process.Start(psi);
            p?.WaitForExit(30000);
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }

    private bool RenombrarWmic(string ip, string nombre, Credenciales cred, bool reiniciar)
    {
        try
        {
            var psi = new ProcessStartInfo("wmic",
                $"/node:{ip} /user:{cred.UsuarioCompleto} /password:{cred.Password} " +
                $"computersystem where \"name!=''\" call rename name=\"{nombre}\"")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = Process.Start(psi);
            string output = p?.StandardOutput.ReadToEnd() ?? "";
            p?.WaitForExit(30000);
            bool ok = output.Contains("ReturnValue = 0");
            if (ok && reiniciar) WmiService.Reiniciar(ip, cred);
            return ok;
        }
        catch { return false; }
    }

    // ── Importar CSV ─────────────────────────────────────────
    public (int agregados, List<string> errores) ImportarCSV(string contenido)
    {
        int agregados = 0;
        var errores   = new List<string>();
        foreach (var linea in contenido.Split('\n').Skip(1))
        {
            var cols = linea.Trim().Split(';');
            if (cols.Length < 3) continue;
            string ip    = cols[0].Trim();
            string act   = cols[1].Trim();
            string nuevo = cols[2].Trim();
            if (string.IsNullOrWhiteSpace(ip) || string.IsNullOrWhiteSpace(nuevo)) continue;
            var (v, m) = ValidarNombre(nuevo);
            if (!v) { errores.Add($"{ip} → '{nuevo}': {m}"); continue; }
            Cola.Add(new ItemRenombrado { IP = ip, NombreActual = act, NuevoNombre = nuevo, Resultado = "Pendiente" });
            agregados++;
        }
        OnCambio?.Invoke();
        return (agregados, errores);
    }

    public string ExportarCSVPlantilla(List<(string ip, string nombre)> equipos)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ip;nombre_actual;nuevo_nombre");
        foreach (var (ip, nombre) in equipos) sb.AppendLine($"{ip};{nombre};");
        return sb.ToString();
    }

    private void GuardarLog(List<ItemRenombrado> items)
    {
        try
        {
            string dir  = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string ruta = Path.Combine(dir, "archivos generados",
                $"log_renombrado_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
            var sb = new StringBuilder("ip;nombre_actual;nuevo_nombre;resultado;metodo\n");
            foreach (var i in items)
                sb.AppendLine($"{i.IP};{i.NombreActual};{i.NuevoNombre};{i.Resultado};{i.Metodo}");
            File.WriteAllText(ruta, sb.ToString());
        }
        catch { }
    }

    private static string EjecutarPS(string cmd)
    {
        try
        {
            var psi = new ProcessStartInfo("powershell", $"-Command \"{cmd}\"")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = Process.Start(psi);
            string o = p?.StandardOutput.ReadToEnd() ?? "";
            p?.WaitForExit(20000);
            return o;
        }
        catch { return ""; }
    }

    private static string EscPS(string s) => s.Replace("'", "''");
}
