using FacturasFacil.Core.Models;

namespace FacturasFacil.Core.Services;

public static class FacturaProcessor
{
    /// <summary>
    /// Procesa todos los archivos comprimidos en una carpeta y genera el Excel en la carpeta de destino.
    /// </summary>
    public static ProcesamientoResultado ProcesarCarpeta(string carpetaOrigen, string carpetaDestino)
    {
        var resultado = new ProcesamientoResultado();
        var archivos = ArchivadorService.ObtenerArchivosComprimidos(carpetaOrigen).ToList();
        resultado.TotalArchivos = archivos.Count;

        foreach (var archivo in archivos)
        {
            try
            {
                var xmls = ArchivadorService.ExtraerXmls(archivo);
                var nombreArchivo = Path.GetFileName(archivo);

                foreach (var (nombre, stream) in xmls)
                {
                    try
                    {
                        using (stream)
                        {
                            var factura = CfdiParser.Parse(stream, nombre);
                            factura.ArchivoOrigen = nombreArchivo;
                            resultado.Facturas.Add(factura);
                        }
                    }
                    catch (Exception ex)
                    {
                        resultado.Errores.Add(new ErrorArchivo
                        {
                            Archivo = $"{nombreArchivo}/{nombre}",
                            Mensaje = ex.Message
                        });
                    }
                }

                resultado.ArchivosExitosos++;
            }
            catch (Exception ex)
            {
                resultado.ArchivosConError++;
                resultado.Errores.Add(new ErrorArchivo
                {
                    Archivo = Path.GetFileName(archivo),
                    Mensaje = ex.Message
                });
            }
        }

        if (resultado.Facturas.Count > 0)
        {
            Directory.CreateDirectory(carpetaDestino);
            var nombreExcel = ExcelGenerator.GenerarNombreArchivo();
            var rutaExcel = Path.Combine(carpetaDestino, nombreExcel);
            var bytes = ExcelGenerator.Generar(resultado.Facturas);
            File.WriteAllBytes(rutaExcel, bytes);
            resultado.RutaExcel = rutaExcel;
        }

        return resultado;
    }

    /// <summary>
    /// Procesa una lista de streams (para uso en la API web).
    /// </summary>
    public static (List<FacturaInfo> Facturas, List<ErrorArchivo> Errores) ProcesarStreams(
        IEnumerable<(string NombreArchivo, Stream Contenido)> archivos)
    {
        var facturas = new List<FacturaInfo>();
        var errores = new List<ErrorArchivo>();

        foreach (var (nombreArchivo, stream) in archivos)
        {
            try
            {
                var xmls = ArchivadorService.ExtraerXmlsDesdeStream(stream, nombreArchivo);
                foreach (var (nombre, xmlStream) in xmls)
                {
                    try
                    {
                        using (xmlStream)
                        {
                            var factura = CfdiParser.Parse(xmlStream, nombre);
                            factura.ArchivoOrigen = nombreArchivo;
                            facturas.Add(factura);
                        }
                    }
                    catch (Exception ex)
                    {
                        errores.Add(new ErrorArchivo
                        {
                            Archivo = $"{nombreArchivo}/{nombre}",
                            Mensaje = ex.Message
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                errores.Add(new ErrorArchivo
                {
                    Archivo = nombreArchivo,
                    Mensaje = ex.Message
                });
            }
        }

        return (facturas, errores);
    }
}
