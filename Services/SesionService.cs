// ============================================================
// Services/SesionService.cs
// Mantiene credenciales y configuración durante la sesión web.
// ============================================================
using AdminToolWeb.Models;

namespace AdminToolWeb.Services;

public class SesionService
{
    public Credenciales? Credenciales { get; private set; }
    public AppConfig     Config       { get; } = new();
    public bool          Autenticado  => Credenciales != null;

    public void GuardarCredenciales(string usuario, string password, string dominio)
    {
        Credenciales = new Credenciales
        {
            Usuario  = usuario,
            Password = password,
            Dominio  = dominio,
        };
    }

    public void LimpiarCredenciales() => Credenciales = null;
}
