using appWeb2.Data;
using appWeb2.Filtros;
using appWeb2.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using Rotativa.AspNetCore;
using appWeb2.Helpers;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using Microsoft.AspNetCore.Session;

namespace appWeb2.Controllers
    {
        public class AccountController : Controller
        {
            private readonly AppDbContext _context;

            public AccountController(AppDbContext context)
                {
                    _context = context;
                }

            public IActionResult Index()
                {
                    return RedirectToAction("Login");
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

            // GET: Account/Register
            public IActionResult Register()
            {
                return View();
            }

            // POST: Account/Register
            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> Register(RegisterViewModel model)
            {
                if (ModelState.IsValid)
                {
                    // Verificar si el correo ya existe
                    var existingUser = await _context.Usuarios
                        .FirstOrDefaultAsync(u => u.correo == model.correo);
                    
                    if (existingUser != null)
                    {
                        ModelState.AddModelError("correo", "Este correo electrónico ya está registrado.");
                        return View(model);
                    }

                    // Generar salt y hash para la contraseña
                    string salt = Guid.NewGuid().ToString();
                    
                    // Generar hash directamente como bytes
                    string saltedPassword = salt + model.PasswordInput;
                    byte[] passwordBytes;
                    
                    using (SHA256 sha256 = SHA256.Create())
                    {
                        byte[] saltedPasswordBytes = Encoding.Unicode.GetBytes(saltedPassword);
                        passwordBytes = sha256.ComputeHash(saltedPasswordBytes);
                    }
                    
                    // Crear nuevo usuario con los datos del ViewModel
                    var usuario = new Usuario
                    {
                        nombre = model.nombre,
                        correo = model.correo,
                        salt = salt,
                        password = passwordBytes,
                        FechaRegistro = DateTime.Now,
                        idRol = model.idRol ?? 2 // Usar rol seleccionado o por defecto Usuario
                    };

                    _context.Add(usuario);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "¡Cuenta creada exitosamente! Por favor inicia sesión.";
                    return RedirectToAction("Login");
                }
                
                return View(model);
            }

            [AuthorizeRole("Admin")]
            public IActionResult Dashboard()
                {
                    var categorias = _context.Categorias.ToList();
                    ViewBag.Categorias = categorias;

                    return View();
                }
        
            public IActionResult ObtenerDatos(int? idcategoria)
            {
                var query = from v in _context.VideoJuegos
                            join c in _context.Categorias
                            on v.idcategoria equals c.idcategoria
                            select new { c.idcategoria, c.categoria };

                if(idcategoria.HasValue && idcategoria > 0)
                {
                    query = query.Where(x => x.idcategoria == idcategoria);
                } 

                var data = query
                    .GroupBy(x => new { x.idcategoria, x.categoria })
                    .Select(g => new
                    {
                        idcategoria = g.Key.idcategoria,
                        categoria = g.Key.categoria,
                        total = g.Count()
                    }).ToList();

                return Json(data);
            }

            public IActionResult ObtenerPromociones(int? idcategoria)
            {
                var query = from v in _context.VideoJuegos
                            join c in _context.Categorias
                            on v.idcategoria equals c.idcategoria
                            select v;

                if(idcategoria.HasValue && idcategoria > 0)
                {
                    query = query.Where(x => x.idcategoria == idcategoria);
                }

                var data = query
                    .GroupBy(x => x.EnPromocion)
                    .Select(g => new
                    {
                        estado = g.Key ? "En Promoción" : "Sin Promoción",
                        total = g.Count()
                    }).ToList();

                return Json(data);
            }

            public IActionResult ObtenerIngresosEstimados(int? idcategoria)
            {
                var query = from v in _context.VideoJuegos
                            join c in _context.Categorias
                            on v.idcategoria equals c.idcategoria
                            select new { v.precio, c.categoria, v.idcategoria };

                if(idcategoria.HasValue && idcategoria > 0)
                {
                    query = query.Where(x => x.idcategoria == idcategoria);
                }

                var data = query
                    .GroupBy(x => new { x.idcategoria, x.categoria })
                    .Select(g => new
                    {
                        idcategoria = g.Key.idcategoria,
                        categoria = g.Key.categoria,
                        cantidad = g.Count(),
                        precioPromedio = g.Average(x => x.precio),
                        ingresoEstimado = g.Count() * g.Average(x => x.precio)
                    })
                    .OrderByDescending(x => x.ingresoEstimado)
                    .ToList();

                return Json(data);
            }

            public IActionResult ObtenerPreciosJuegos(int? idcategoria)
            {
                var query = from v in _context.VideoJuegos
                            join c in _context.Categorias
                            on v.idcategoria equals c.idcategoria
                            select new { v.titulo, v.precio, c.categoria, v.idcategoria };

                if(idcategoria.HasValue && idcategoria > 0)
                {
                    query = query.Where(x => x.idcategoria == idcategoria);
                }

                var data = query
                    .OrderBy(x => x.titulo)
                    .Select(x => new
                    {
                        titulo = x.titulo,
                        precio = x.precio,
                        categoria = x.categoria
                    }).ToList();

                return Json(data);
            }
            public IActionResult Login ()
                {
                return View();
                }


            public async Task<IActionResult> DetalleVentas(DateTime? desde, DateTime? hasta, int? clienteId, int? videojuegoId, int pagina=1)
            {
                int paginador = 10;

                var query = _context.detalle_compra
                        .Include(d => d.Compra)
                        .Include(d => d.VideoJuegos)
                        .Include(d => d.Compra.Usuario)
                        .AsQueryable();
            
                if(desde.HasValue)
                {
                    query = query.Where(x => x.fechaHoraTransaccion >= desde.Value);
                }

                if(hasta.HasValue)
                {
                    query = query.Where(x => x.fechaHoraTransaccion <= hasta);
                }

                if(clienteId.HasValue && clienteId > 0)
                {
                    query = query.Where(x => x.Compra.UsuarioId == clienteId.Value);
                }

                if(videojuegoId.HasValue && videojuegoId > 0)
                {
                    query = query.Where(x => x.VideoJuegosId == videojuegoId.Value);
                }

                var datos = await query
                    .OrderByDescending(x => x.fechaHoraTransaccion)
                    .Skip((pagina - 1) * paginador)
                    .Take(paginador)
                    .Select(x => new VentaViewModel
                    {
                        idCompra = x.idCompra,
                        VideoJuegosId = x.VideoJuegosId,
                        cantidad = x.cantidad,
                        total = x.total,
                        estadoCompra = x.estadoCompra,
                        fechaHoraTransaccion = x.fechaHoraTransaccion,
                        codigoTransaccion = x.codigoTransaccion,
                        NombreUsuario = x.Compra.Usuario.nombre,
                        NombreVideojuego = x.VideoJuegos.titulo
                    })
                    .ToListAsync(); 

                    ViewBag.TotalPaginas = (int)Math.Ceiling((double)query.Count() / paginador);
                    ViewBag.PaginaActual = pagina;
                    ViewBag.Desde = desde;
                    ViewBag.Hasta = hasta;
                    ViewBag.ClienteId = clienteId;
                    ViewBag.VideojuegoId = videojuegoId;

                // Cargar datos para los select de filtros
                ViewBag.Clientes = await _context.Usuarios
                    .OrderBy(u => u.nombre)
                    .Select(u => new { u.Id, u.nombre })
                    .ToListAsync();

                ViewBag.Videojuegos = await _context.VideoJuegos
                    .OrderBy(v => v.titulo)
                    .Select(v => new { v.Id, v.titulo })
                    .ToListAsync();

                return View(datos);
            }


        public async Task<IActionResult> MisJuegos(DateTime? desde, DateTime? hasta, string nombreJuego)
        {
            var usuarioIdStr = HttpContext.Session.GetString("UsuarioId");
            
            if (string.IsNullOrEmpty(usuarioIdStr) || !int.TryParse(usuarioIdStr, out int usuarioId))
            {
                return RedirectToAction("Login", "Account");
            }

            var query = _context.detalle_compra
                .Include(d => d.VideoJuegos)
                .ThenInclude(v => v.Categoria)
                .Where(d => d.Compra.UsuarioId == usuarioId)
                .Select(d => d.VideoJuegos)
                .Distinct()
                .AsQueryable();

            // Filtro por nombre de videojuego
            if (!string.IsNullOrEmpty(nombreJuego))
            {
                query = query.Where(v => v.titulo.Contains(nombreJuego));
            }

            // Filtro por fecha de compra (desde)
            if (desde.HasValue)
            {
                query = query.Where(v => _context.detalle_compra
                    .Any(d => d.VideoJuegosId == v.Id && 
                              d.Compra.UsuarioId == usuarioId && 
                              d.fechaHoraTransaccion >= desde.Value));
            }

            // Filtro por fecha de compra (hasta)
            if (hasta.HasValue)
            {
                query = query.Where(v => _context.detalle_compra
                    .Any(d => d.VideoJuegosId == v.Id && 
                              d.Compra.UsuarioId == usuarioId && 
                              d.fechaHoraTransaccion <= hasta.Value));
            }

            var juegosComprados = await query.ToListAsync();

            ViewBag.Desde = desde;
            ViewBag.Hasta = hasta;
            ViewBag.NombreJuego = nombreJuego;

            return View(juegosComprados);
        }

        public async Task<IActionResult> ExportarMisJuegosPDF()
        {
            var usuarioIdStr = HttpContext.Session.GetString("UsuarioId");
            
            if (string.IsNullOrEmpty(usuarioIdStr) || !int.TryParse(usuarioIdStr, out int usuarioId))
            {
                return RedirectToAction("Login", "Account");
            }

            // Obtener los videojuegos comprados por el usuario
            var juegosComprados = await _context.detalle_compra
                .Include(d => d.VideoJuegos)
                .ThenInclude(v => v.Categoria)
                .Where(d => d.Compra.UsuarioId == usuarioId)
                .Select(d => d.VideoJuegos)
                .Distinct()
                .ToListAsync();

            ViewBag.NombreUsuario = HttpContext.Session.GetString("usuario");
            
            return new ViewAsPdf("PdfMisJuegos", juegosComprados)
            {
                FileName = PdfHelper.GenerarNombrePdfUnico("MisVideojuegos", HttpContext.Session.GetString("usuario") ?? "Usuario"),
            };
        }

        public async Task<IActionResult> ExportarPDF(DateTime? desde, DateTime? hasta)
        {
            var query = _context.detalle_compra
                    .Include(d => d.Compra)
                    .Include(c => c.VideoJuegos)
                    .Include(d => d.Compra.Usuario)
                    .AsQueryable();

                if(desde.HasValue)
                {
                    query = query.Where(d => d.fechaHoraTransaccion >= desde);
                }

                if(hasta.HasValue)
                {
                    query = query.Where(d => d.fechaHoraTransaccion <= hasta);
                }
                var datos = await query
                    .OrderByDescending(d => d.fechaHoraTransaccion)
                    .Select(d => new VentaViewModel
                    {
                        idCompra = d.idCompra,
                        VideoJuegosId = d.VideoJuegosId,
                        cantidad = d.cantidad,
                        total = d.total,
                        estadoCompra = d.estadoCompra,
                        fechaHoraTransaccion = d.fechaHoraTransaccion,
                        codigoTransaccion = d.codigoTransaccion,
                        NombreUsuario = d.Compra.Usuario.nombre,
                        NombreVideojuego = d.VideoJuegos.titulo
                    }).ToListAsync(); 

                return new ViewAsPdf("PdfVentas", datos)
                {
                    FileName = PdfHelper.GenerarNombrePdfUnico("Ventas", "Reporte"),
                };
        }



            [HttpPost]
            [ValidateAntiForgeryToken]
            public IActionResult Login(Login model)
            {
                var user = _context.Usuarios
                    .Include(u => u.Rol)
                    .FirstOrDefault(u => u.correo == model.correo);

                if (user != null)
                {
                    string saltedPassword = user.salt + model.password;

                    using (SHA256 sha256 = SHA256.Create())
                    {
                        byte[] inputBytes = Encoding.Unicode.GetBytes(saltedPassword);
                        byte[] hashBytes = sha256.ComputeHash(inputBytes);
                        Console.WriteLine("Password input: " + model.password);
                        Console.WriteLine("Salt DB: " + user.salt);
                        Console.WriteLine("Concatenado: " + saltedPassword);

                        Console.WriteLine("Hash generado: " + Convert.ToBase64String(hashBytes));
                        Console.WriteLine("Hash DB: " + Convert.ToBase64String(user.password));
                        Console.WriteLine("Hash DB Length: " + user.password.Length);
                        Console.WriteLine("Hash Generated Length: " + hashBytes.Length);

                        // Intentar comparar directamente (nuevo formato)
                        bool passwordMatch = hashBytes.SequenceEqual(user.password);
                        
                        // Si no coincide, intentar con formato antiguo (Base64)
                        if (!passwordMatch && user.password != null)
                        {
                            try
                            {
                                string storedPasswordBase64 = Convert.ToBase64String(user.password);
                                byte[] storedHashBytes = Convert.FromBase64String(storedPasswordBase64);
                                passwordMatch = hashBytes.SequenceEqual(storedHashBytes);
                            }
                            catch
                            {
                                // Si falla la conversión, continuar con false
                            }
                        }
                        
                        if (passwordMatch)
                        {
                            HttpContext.Session.SetString("usuario", user.nombre);
                            HttpContext.Session.SetString("UsuarioId", user.Id.ToString());

                            if (user.Rol != null)
                            {
                                HttpContext.Session.SetString("rol", user.Rol.rol);
                            }

                            if (user.Rol != null && user.Rol.rol == "Admin")
                            {
                                return RedirectToAction("Dashboard", "Account");
                            }
                            else
                            {
                                return RedirectToAction("JuegosVentas", "VideoJuegos");
                            }
                        }
                    }

                }

                ViewBag.Error = "Credenciales incorrectos";
                return View();
            }

            public IActionResult Logout()
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login");
            }

            public IActionResult AccessDenied()
            {
                return View();
            }

        }
}
    
