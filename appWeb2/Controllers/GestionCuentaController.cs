using appWeb2.Data;
using appWeb2.Filtros;
using appWeb2.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace appWeb2.Controllers
{
    [AuthorizeRole("Usuario")]
    public class GestionCuentaController : Controller
    {
        private readonly AppDbContext _context;

        public GestionCuentaController(AppDbContext context)
        {
            _context = context;
        }

        // GET: GestionCuenta
        public async Task<IActionResult> Index()
        {
            var usuarioIdStr = HttpContext.Session.GetString("UsuarioId");
            
            if (string.IsNullOrEmpty(usuarioIdStr) || !int.TryParse(usuarioIdStr, out int usuarioId))
            {
                return RedirectToAction("Login", "Account");
            }

            var usuario = await _context.Usuarios
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.Id == usuarioId);

            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            return View(usuario);
        }

        // GET: GestionCuenta/Edit
        public async Task<IActionResult> Edit()
        {
            var usuarioIdStr = HttpContext.Session.GetString("UsuarioId");
            
            if (string.IsNullOrEmpty(usuarioIdStr) || !int.TryParse(usuarioIdStr, out int usuarioId))
            {
                return RedirectToAction("Login", "Account");
            }

            var usuario = await _context.Usuarios.FindAsync(usuarioId);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // No enviar la contraseña actual a la vista
            usuario.password = null;

            return View(usuario);
        }

        // POST: GestionCuenta/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Usuario usuario)
        {
            var usuarioIdStr = HttpContext.Session.GetString("UsuarioId");
            
            if (string.IsNullOrEmpty(usuarioIdStr) || !int.TryParse(usuarioIdStr, out int usuarioId))
            {
                return RedirectToAction("Login", "Account");
            }

            if (usuarioId != usuario.Id)
            {
                return RedirectToAction("Login", "Account");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var usuarioOriginal = await _context.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == usuarioId);
                    
                    if (usuarioOriginal == null)
                    {
                        return RedirectToAction("Login", "Account");
                    }

                    // Actualizar nombre
                    usuarioOriginal.nombre = usuario.nombre;

                    // Si se proporcionó una nueva contraseña, actualizarla
                    if (usuario.password != null && usuario.password.Length > 0)
                    {
                        string salt = Guid.NewGuid().ToString();
                        string passwordStr = Encoding.Unicode.GetString(usuario.password);
                        string hashedPassword = HashPassword(passwordStr, salt);
                        usuarioOriginal.salt = salt;
                        usuarioOriginal.password = Convert.FromBase64String(hashedPassword);
                    }

                    _context.Update(usuarioOriginal);
                    await _context.SaveChangesAsync();

                    // Actualizar sesión con el nuevo nombre
                    HttpContext.Session.SetString("usuario", usuarioOriginal.nombre);

                    TempData["Success"] = "Tu cuenta ha sido actualizada exitosamente.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await UsuarioExists(usuario.Id))
                    {
                        return RedirectToAction("Login", "Account");
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            // Si hay errores, limpiar la contraseña para no mostrarla
            usuario.password = null;
            return View(usuario);
        }

        private async Task<bool> UsuarioExists(int id)
        {
            return await _context.Usuarios.AnyAsync(e => e.Id == id);
        }

        private string HashPassword(string password, string salt)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] saltedPassword = Encoding.Unicode.GetBytes(salt + password);
                byte[] hashBytes = sha256.ComputeHash(saltedPassword);
                return Convert.ToBase64String(hashBytes);
            }
        }
    }
}
