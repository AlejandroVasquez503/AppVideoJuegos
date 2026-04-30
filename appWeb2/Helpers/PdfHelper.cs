using System.IO;
using System.Threading.Tasks;

namespace appWeb2.Helpers
{
    public static class PdfHelper
    {
        private static readonly string DirectorioPdf = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "pdfs");
        private static readonly string ArchivoContador = Path.Combine(DirectorioPdf, "contador_pdf.txt");

        static PdfHelper()
        {
            if (!Directory.Exists(DirectorioPdf))
            {
                Directory.CreateDirectory(DirectorioPdf);
            }

            if (!File.Exists(ArchivoContador))
            {
                File.WriteAllText(ArchivoContador, "0");
            }
        }

        public static int ObtenerSiguienteNumeroPdf()
        {
            try
            {
                int contadorActual = int.Parse(File.ReadAllText(ArchivoContador));
                int siguienteNumero = contadorActual + 1;
                
                File.WriteAllText(ArchivoContador, siguienteNumero.ToString());
                
                return siguienteNumero;
            }
            catch
            {
                return 1;
            }
        }

        public static string GenerarNombrePdfUnico(string nombreBase)
        {
            int numeroPdf = ObtenerSiguienteNumeroPdf();
            return $"{nombreBase}_{numeroPdf:D3}.pdf";
        }

        public static string GenerarNombrePdfUnico(string nombreBase, string sufijo)
        {
            int numeroPdf = ObtenerSiguienteNumeroPdf();
            return $"{nombreBase}_{sufijo}_{numeroPdf:D3}.pdf";
        }
    }
}
