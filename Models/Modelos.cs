// ============================================================
// Models/Modelos.cs — Modelos de datos compartidos
// ============================================================
namespace AdminToolWeb.Models;

public class Credenciales
{
    public string Usuario  { get; set; } = "";
    public string Password { get; set; } = "";
    public string Dominio  { get; set; } = "ejemplo.local";
    public string UsuarioCompleto =>
        string.IsNullOrWhiteSpace(Dominio) ? Usuario : $@"{Dominio}\{Usuario}";
}

public class EquipoInfo
{
    public string IP           { get; set; } = "";
    public string Hostname     { get; set; } = "";
    public string Serial       { get; set; } = "No disponible";
    public string Fabricante   { get; set; } = "Desconocido";
    public string Modelo       { get; set; } = "Desconocido";
    public string SO           { get; set; } = "Desconocido";
    public string VersionSO    { get; set; } = "Desconocido";
    public double RamGB        { get; set; } = 0;
    public string Dominio      { get; set; } = "N/A";
    public string Usuario      { get; set; } = "N/A";
    public string Tipo         { get; set; } = "Desconocido";
    public string Servicios    { get; set; } = "";
    public string FechaEscaneo { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    public string EstadoWMI    { get; set; } = "";
}

public class ItemRenombrado
{
    public string IP           { get; set; } = "";
    public string NombreActual { get; set; } = "";
    public string NuevoNombre  { get; set; } = "";
    public string Resultado    { get; set; } = "Pendiente";
    public string Metodo       { get; set; } = "";
    public string Error        { get; set; } = "";
    public bool   Valido       => Resultado == "OK";
}

public class DiagnosticoResult
{
    public string IP       { get; set; } = "";
    public bool Ping       { get; set; }
    public bool WinRM      { get; set; }
    public bool WinRMS     { get; set; }
    public bool SMB        { get; set; }
    public bool DCOM       { get; set; }
    public bool RDP        { get; set; }
}

public class LogEntry
{
    public DateTime Fecha   { get; set; } = DateTime.Now;
    public string   Nivel   { get; set; } = "INFO";  // OK | ERROR | WARN | INFO
    public string   Mensaje { get; set; } = "";
}

public class AppConfig
{
    public string RedBase    { get; set; } = "192.168.1";
    public string Dominio    { get; set; } = "ejemplo.local";
    public string PsExecPath { get; set; } = @"C:\PSTools\PsExec.exe";
    public string ScriptDir  { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "archivos generados");
}
