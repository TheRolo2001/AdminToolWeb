// ============================================================
// Services/WmiService.cs — WMI nativo via System.Management
// ============================================================
using System.Management;
using AdminToolWeb.Models;

namespace AdminToolWeb.Services;

public static class WmiService
{
    private static ManagementScope? Conectar(string ip, Credenciales cred)
    {
        try
        {
            var opts = new ConnectionOptions
            {
                Username         = cred.UsuarioCompleto,
                Password         = cred.Password,
                Impersonation    = ImpersonationLevel.Impersonate,
                Authentication   = AuthenticationLevel.PacketPrivacy,
                EnablePrivileges = true,
                Timeout          = TimeSpan.FromSeconds(15),
            };
            var scope = new ManagementScope($@"\\{ip}\root\cimv2", opts);
            scope.Connect();
            return scope.IsConnected ? scope : null;
        }
        catch { return null; }
    }

    private static string Query(ManagementScope scope, string wql, string campo)
    {
        try
        {
            using var s = new ManagementObjectSearcher(scope, new ObjectQuery(wql));
            foreach (ManagementObject o in s.Get())
                return o[campo]?.ToString() ?? "Desconocido";
        }
        catch { }
        return "Desconocido";
    }

    public static EquipoInfo? ObtenerInfo(string ip, Credenciales cred)
    {
        var scope = Conectar(ip, cred);
        if (scope == null) return null;
        try
        {
            string ramStr = Query(scope, "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem", "TotalPhysicalMemory");
            double ramGb  = double.TryParse(ramStr, out double r) ? Math.Round(r / (1024.0 * 1024 * 1024), 2) : 0;
            string usuario = Query(scope, "SELECT UserName FROM Win32_ComputerSystem", "UserName");
            return new EquipoInfo
            {
                Serial     = Query(scope, "SELECT SerialNumber FROM Win32_BIOS", "SerialNumber"),
                Fabricante = Query(scope, "SELECT Manufacturer FROM Win32_ComputerSystem", "Manufacturer"),
                Modelo     = Query(scope, "SELECT Model FROM Win32_ComputerSystem", "Model"),
                SO         = Query(scope, "SELECT Caption FROM Win32_OperatingSystem", "Caption"),
                VersionSO  = Query(scope, "SELECT Version FROM Win32_OperatingSystem", "Version"),
                RamGB      = ramGb,
                Dominio    = Query(scope, "SELECT Domain FROM Win32_ComputerSystem", "Domain"),
                Usuario    = usuario == "Desconocido" ? "N/A" : usuario,
            };
        }
        catch { return null; }
    }

    public static string? ObtenerNombre(string ip, Credenciales cred)
    {
        var scope = Conectar(ip, cred);
        if (scope == null) return null;
        string n = Query(scope, "SELECT Name FROM Win32_ComputerSystem", "Name");
        return n == "Desconocido" ? null : n;
    }

    public static bool Renombrar(string ip, string nuevoNombre, Credenciales cred, bool reiniciar)
    {
        var scope = Conectar(ip, cred);
        if (scope == null) return false;
        try
        {
            using var s = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM Win32_ComputerSystem"));
            foreach (ManagementObject cs in s.Get())
            {
                var inp = cs.GetMethodParameters("Rename");
                inp["Name"]     = nuevoNombre;
                inp["Password"] = cred.Password;
                inp["UserName"] = cred.UsuarioCompleto;
                var outp = cs.InvokeMethod("Rename", inp, null);
                bool ok = (uint)(outp["ReturnValue"] ?? 99u) == 0;
                if (ok && reiniciar) Reiniciar(ip, cred);
                return ok;
            }
        }
        catch { }
        return false;
    }

    public static bool Reiniciar(string ip, Credenciales cred)
    {
        var scope = Conectar(ip, cred);
        if (scope == null) return false;
        try
        {
            using var s = new ManagementObjectSearcher(scope,
                new ObjectQuery("SELECT * FROM Win32_OperatingSystem WHERE Primary=TRUE"));
            foreach (ManagementObject os in s.Get()) { os.InvokeMethod("Reboot", null); return true; }
        }
        catch { }
        return false;
    }
}
