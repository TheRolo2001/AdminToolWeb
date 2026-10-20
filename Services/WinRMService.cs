// ============================================================
// Services/WinRMService.cs
// ============================================================
using System.Collections.Concurrent;
using System.Diagnostics;
using AdminToolWeb.Models;

namespace AdminToolWeb.Services;

public class WinRMService
{
    public List<LogEntry> Log       { get; private set; } = [];
    public bool           Ejecutando { get; private set; }
    public event Action?  OnCambio;

    private void L(string nivel, string msg)
    {
        Log.Insert(0, new LogEntry { Nivel = nivel, Mensaje = msg });
        OnCambio?.Invoke();
    }

    public async Task HabilitarEnIpsAsync(List<string> ips, AppConfig cfg, Credenciales cred)
    {
        Ejecutando = true;
        Log.Clear();
        ConfigurarTrustedHosts();
        OnCambio?.Invoke();

        await Task.Run(() =>
        {
            foreach (string ip in ips)
            {
                if (!RedService.Ping(ip)) { L("WARN", $"{ip} — No responde"); continue; }
                if (RedService.WinRmDisponible(ip)) { L("OK", $"{ip} — Ya habilitado"); continue; }
                bool ok = HabilitarViaPsExec(ip, cfg.PsExecPath, cred);
                if (ok)
                {
                    bool verificado = EsperarWinRM(ip);
                    L(verificado ? "OK" : "WARN",
                      $"{ip} — {(verificado ? "Configurado y funcionando" : "Configurado (verificar)")}");
                }
                else L("ERROR", $"{ip} — Falló la configuración");
            }
        });

        Ejecutando = false;
        OnCambio?.Invoke();
    }

    public async Task<DiagnosticoResult> DiagnosticarAsync(string ip)
    {
        return await Task.Run(() => new DiagnosticoResult
        {
            IP     = ip,
            Ping   = RedService.Ping(ip),
            WinRM  = RedService.PuertoAbierto(ip, 5985),
            WinRMS = RedService.PuertoAbierto(ip, 5986),
            SMB    = RedService.PuertoAbierto(ip, 445),
            DCOM   = RedService.PuertoAbierto(ip, 135),
            RDP    = RedService.PuertoAbierto(ip, 3389),
        });
    }

    private void ConfigurarTrustedHosts()
    {
        try
        {
            var psi = new ProcessStartInfo("powershell",
                "-Command \"Set-Item WSMan:\\localhost\\Client\\TrustedHosts -Value '*' -Force; Restart-Service WinRM -Force\"")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = Process.Start(psi);
            p?.WaitForExit(20000);
            L(p?.ExitCode == 0 ? "OK" : "WARN", "TrustedHosts configurado localmente");
        }
        catch (Exception ex) { L("WARN", $"TrustedHosts: {ex.Message}"); }
    }

    private bool HabilitarViaPsExec(string ip, string psExecPath, Credenciales cred)
    {
        if (!File.Exists(psExecPath)) { L("ERROR", $"PsExec no encontrado: {psExecPath}"); return false; }
        string ps = "Enable-PSRemoting -SkipNetworkProfileCheck -Force; " +
                    "Set-Item WSMan:\\\\localhost\\\\Client\\\\TrustedHosts -Value '*' -Force; " +
                    "Restart-Service WinRM";
        try
        {
            var psi = new ProcessStartInfo(psExecPath,
                $@"\\{ip} -u {cred.UsuarioCompleto} -p {cred.Password} -accepteula -s powershell.exe -Command ""{ps}""")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = Process.Start(psi);
            p?.WaitForExit(60000);
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }

    private bool EsperarWinRM(string ip, int intentos = 6)
    {
        for (int i = 0; i < intentos; i++)
        {
            if (RedService.WinRmDisponible(ip)) return true;
            Thread.Sleep(5000);
        }
        return false;
    }
}
