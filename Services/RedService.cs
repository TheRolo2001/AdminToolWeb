// ============================================================
// Services/RedService.cs — Utilidades de red
// ============================================================
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace AdminToolWeb.Services;

public static class RedService
{
    public static int TimeoutPingMs = 1000;
    public static int TimeoutPortMs = 500;
    public static int MaxWorkers    = 50;

    public static bool Ping(string ip)
    {
        try
        {
            using var ping = new Ping();
            return ping.Send(ip, TimeoutPingMs).Status == IPStatus.Success;
        }
        catch { return false; }
    }

    public static bool PuertoAbierto(string ip, int puerto)
    {
        try
        {
            using var client = new TcpClient();
            var r = client.BeginConnect(ip, puerto, null, null);
            bool ok = r.AsyncWaitHandle.WaitOne(TimeoutPortMs);
            if (ok) client.EndConnect(r);
            return ok;
        }
        catch { return false; }
    }

    public static bool WinRmDisponible(string ip) => PuertoAbierto(ip, 5985);
    public static bool SmbDisponible(string ip)   => PuertoAbierto(ip, 445) || PuertoAbierto(ip, 135);

    public static string ObtenerHostname(string ip)
    {
        try { return Dns.GetHostEntry(ip).HostName.Split('.')[0]; }
        catch { return "Desconocido"; }
    }

    public static string NormalizarIp(string entrada, string redBase)
    {
        entrada = entrada.Trim();
        return entrada.Contains('.') ? entrada : $"{redBase}.{entrada}";
    }

    private static readonly Dictionary<int, string> NombreServicio = new()
    {
        {80,"HTTP"},{443,"HTTPS"},{22,"SSH"},{3389,"RDP"},
        {445,"SMB"},{135,"RPC"},{139,"NetBIOS"},{21,"FTP"},
        {3306,"MySQL"},{5432,"PostgreSQL"},
    };

    public static List<string> DetectarServicios(string ip)
    {
        var activos = new List<string>();
        Parallel.ForEach(NombreServicio, kvp =>
        {
            if (PuertoAbierto(ip, kvp.Key))
                lock (activos) activos.Add($"{kvp.Value}({kvp.Key})");
        });
        activos.Sort();
        return activos;
    }

    public static string DeducirTipo(List<string> servicios)
    {
        string s = string.Join(",", servicios);
        if (s.Contains("HTTP") || s.Contains("HTTPS")) return "Servidor Web";
        if (s.Contains("SMB"))                          return "Servidor Archivos";
        if (s.Contains("RDP"))                          return "Estación de Trabajo";
        return "Desconocido";
    }
}
