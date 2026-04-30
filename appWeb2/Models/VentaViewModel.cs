namespace appWeb2.Models
{
    public class VentaViewModel
    {
        public int idCompra { get; set; }
        public DateTime FechaCompra { get; set; }
        public int UsuarioId { get; set; }
        public int VideoJuegosId { get; set; }
        public int cantidad { get; set; }
        public decimal total { get; set; }
        public string estadoCompra { get; set; }
        public DateTime fechaHoraTransaccion { get; set; }
        public string codigoTransaccion { get; set; }
        public string NombreUsuario { get; set; }
        public string NombreVideojuego { get; set; }
    }
}
