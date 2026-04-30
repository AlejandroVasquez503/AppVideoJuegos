using Microsoft.AspNetCore.Mvc;
using appWeb2.Services;
using appWeb2.Data;
using appWeb2.Models;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace appWeb2.Controllers
{
    public class PaymentController : Controller
    {
        private readonly PayPalService _paypalService;
        private readonly AppDbContext _context;

        public PaymentController(PayPalService paypalService, AppDbContext context)
        {
            _paypalService = paypalService;
            _context = context;
        }

        /// <summary>
        /// Iniciar proceso de pago con PayPal
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] PaymentRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest("Datos inválidos");

            try
            {
                var returnUrl = Url.Action("ApproveOrder", "Payment", null, Request.Scheme, Request.Host.ToString());
                var cancelUrl = Url.Action("CancelOrder", "Payment", null, Request.Scheme, Request.Host.ToString());

                var orderId = await _paypalService.CreateOrderAsync(
                    request.Amount,
                    request.Description ?? "Compra de videojuegos",
                    returnUrl,
                    cancelUrl
                );

                return Json(new { id = orderId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en CreateOrder: {ex.Message}");
                return BadRequest(new { error = "Error al procesar la orden de pago" });
            }
        }

        /// <summary>
        /// Usuario aprobó el pago en PayPal - Capturar orden
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ApproveOrder(string token)
        {
            Console.WriteLine($"=== ApproveOrder iniciado con token: {token} ===");
            
            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("ERROR: Token nulo o vacío");
                return BadRequest("Token no válido");
            }

            try
            {
                Console.WriteLine("Intentando capturar orden en PayPal...");
                var orderJson = await _paypalService.CaptureOrderAsync(token);
                Console.WriteLine($"Orden capturada: {orderJson}");

                // Validar estado del pago
                if (orderJson.TryGetProperty("status", out var statusEl))
                {
                    string status = statusEl.GetString();
                    Console.WriteLine($"Estado del pago: {status}");
                    
                    if (status != "COMPLETED")
                    {
                        Console.WriteLine($"ERROR: Estado no completado: {status}");
                        return RedirectToAction("PaymentFailed");
                    }
                }
                else
                {
                    Console.WriteLine("ERROR: No se encontró propiedad 'status' en la respuesta");
                    return RedirectToAction("PaymentFailed");
                }

                Console.WriteLine("Guardando información de la compra en BD...");
                await SavePurchaseAsync(orderJson, token);
                Console.WriteLine("Compra guardada exitosamente");

                Console.WriteLine("Redirigiendo a PaymentSuccess...");
                return RedirectToAction("PaymentSuccess", new { orderId = token });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR en ApproveOrder: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                return RedirectToAction("PaymentFailed", new { error = "Error al procesar el pago aprobado" });
            }
        }

        /// <summary>
        /// Usuario canceló el pago en PayPal
        /// </summary>
        [HttpGet]
        public IActionResult CancelOrder()
        {
            return RedirectToAction("PaymentCancelled");
        }

        /// <summary>
        /// Página de éxito
        /// </summary>
        [HttpGet]
        public IActionResult PaymentSuccess(string orderId)
        {
            ViewBag.OrderId = orderId;
            ViewBag.Message = "¡Pago realizado exitosamente! Tu compra ha sido procesada.";
            return View();
        }

        /// <summary>
        /// Página de fallo
        /// </summary>
        [HttpGet]
        public IActionResult PaymentFailed(string error = "")
        {
            ViewBag.Error = error ?? "Hubo un error procesando tu pago.";
            return View();
        }

        /// <summary>
        /// Página de cancelación
        /// </summary>
        [HttpGet]
        public IActionResult PaymentCancelled()
        {
            ViewBag.Message = "El pago fue cancelado.";
            return View();
        }

        /// <summary>
        /// Acción de compra desde el home - obtiene datos del juego y redirige a pago
        /// </summary>
        [HttpGet]
        public IActionResult ComprarJuego(int juegoId)
        {
            try
            {
                // Obtener datos del juego desde la base de datos
                var juego = _context.VideoJuegos
                    .Include(v => v.Categoria)
                    .FirstOrDefault(v => v.Id == juegoId);

                if (juego == null)
                {
                    return NotFound("Videojuego no encontrado");
                }

                // Guardar datos en sesión para el proceso de pago
                HttpContext.Session.SetString("JuegoId", juego.Id.ToString());
                HttpContext.Session.SetString("Cantidad", "1");
                
                // Redirigir a PagoSimple con los datos reales del juego
                return RedirectToAction("PagoSimple", new { 
                    juegoId = juego.Id, 
                    monto = juego.precio 
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en ComprarJuego: {ex.Message}");
                return RedirectToAction("Error", "Home");
            }
        }

        /// <summary>
        /// Página de pago simple para prueba (sin carrito)
        /// </summary>
        [HttpGet]
        public IActionResult PagoSimple(int juegoId = 1, decimal monto = 59.99m)
        {
            try
            {
                // Obtener datos del juego desde la base de datos
                var juego = _context.VideoJuegos
                    .Include(v => v.Categoria)
                    .FirstOrDefault(v => v.Id == juegoId);

                if (juego != null)
                {
                    // Usar datos reales del juego
                    ViewBag.JuegoId = juego.Id;
                    ViewBag.Monto = juego.precio;
                    ViewBag.NombreJuego = juego.titulo;
                    ViewBag.Descripcion = juego.descripcion;
                    ViewBag.Categoria = juego.Categoria?.categoria;
                    ViewBag.Imagen = juego.imagen;
                }
                else
                {
                    // Valores por defecto si no se encuentra el juego
                    ViewBag.JuegoId = juegoId;
                    ViewBag.Monto = monto;
                    ViewBag.NombreJuego = "Videojuego no encontrado";
                    ViewBag.Descripcion = "";
                    ViewBag.Categoria = "";
                    ViewBag.Imagen = "";
                }
                
                return View();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en PagoSimple: {ex.Message}");
                ViewBag.JuegoId = juegoId;
                ViewBag.Monto = monto;
                ViewBag.NombreJuego = "Error cargando datos";
                ViewBag.Descripcion = "";
                ViewBag.Categoria = "";
                ViewBag.Imagen = "";
                return View();
            }
        }

        /// <summary>
        /// Guardar detalles de la compra en la BD
        /// </summary>
        private async Task SavePurchaseAsync(JsonElement paypalOrder, string transactionId)
        {
            try
            {
                // Obtener el UsuarioId de la sesión
                var userIdStr = HttpContext.Session.GetString("UsuarioId");
                if (!int.TryParse(userIdStr, out int usuarioId))
                {
                    Console.WriteLine("WARNING: No hay UsuarioId en sesión, usando usuario por defecto para pruebas");
                    usuarioId = 1;
                }

                var juegoIdStr = HttpContext.Session.GetString("JuegoId");
                var cantidadStr = HttpContext.Session.GetString("Cantidad");
                
                if (!int.TryParse(juegoIdStr, out int videoJuegosId))
                    videoJuegosId = 1;
                    
                if (!int.TryParse(cantidadStr, out int cantidad))
                    cantidad = 1;

                // Crear compra
                var compra = new Compra
                {
                    UsuarioId = usuarioId,
                    FechaCompra = DateTime.Now
                };

                _context.Compras.Add(compra);
                await _context.SaveChangesAsync();

                decimal total = 0;
                if (paypalOrder.TryGetProperty("purchase_units", out var purchaseUnits) && purchaseUnits.ValueKind == System.Text.Json.JsonValueKind.Array && purchaseUnits.GetArrayLength() > 0)
                {
                    var firstUnit = purchaseUnits[0];
                    if (firstUnit.TryGetProperty("amount", out var amountObj) && amountObj.TryGetProperty("value", out var valueEl))
                    {
                        string valorStr = valueEl.GetString();
                        Console.WriteLine($"Valor extraído de PayPal: {valorStr}");
                        if (decimal.TryParse(valorStr, out decimal montoParseado))
                        {
                            total = montoParseado;
                            Console.WriteLine($"Monto parseado exitosamente: {total}");
                        }
                        else
                        {
                            Console.WriteLine($"Error parseando monto: {valorStr}");
                        }
                    }
                }
                
                if (total == 0)
                {
                    var juego = _context.VideoJuegos.FirstOrDefault(v => v.Id == videoJuegosId);
                    if (juego != null)
                    {
                        total = juego.precio;
                        Console.WriteLine($"Usando precio del juego como fallback: {total}");
                    }
                }

                var detalleCompra = new DetalleCompra
                {
                    idCompra = compra.Id,
                    VideoJuegosId = videoJuegosId,
                    cantidad = cantidad,
                    total = total,
                    estadoCompra = "Pagado",
                    fechaHoraTransaccion = DateTime.Now,
                    codigoTransaccion = transactionId
                };

                _context.detalle_compra.Add(detalleCompra);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error guardando compra: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// Modelo para la solicitud de pago
    /// </summary>
    public class PaymentRequest
    {
        public decimal Amount { get; set; }
        public string Description { get; set; }
    }
}
