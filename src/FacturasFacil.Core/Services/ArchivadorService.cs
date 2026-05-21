using System.IO.Compression;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace FacturasFacil.Core.Services;

public static class ArchivadorService
{
    private static readonly string[] ExtensionesComprimidas = [".zip", ".rar"];

    public static List<(string Nombre, Stream Contenido)> ExtraerXmls(string rutaArchivo)
    {
        var ext = Path.GetExtension(rutaArchivo).ToLowerInvariant();
        return ext switch
        {
            ".zip" => ExtraerXmlsZip(File.OpenRead(rutaArchivo)),
            ".rar" => ExtraerXmlsRar(rutaArchivo),
            _ => throw new NotSupportedException($"Formato no soportado: {ext}. Use ZIP o RAR.")
        };
    }

    public static List<(string Nombre, Stream Contenido)> ExtraerXmlsDesdeStream(Stream stream, string nombreArchivo)
    {
        var ext = Path.GetExtension(nombreArchivo).ToLowerInvariant();
        if (ext == ".zip")
            return ExtraerXmlsZip(stream);

        if (ext == ".rar")
        {
            // RAR desde stream requiere archivo temporal porque la biblioteca necesita seek
            var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".rar");
            try
            {
                using (var fs = File.Create(tmp))
                    stream.CopyTo(fs);
                return ExtraerXmlsRar(tmp);
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }

        throw new NotSupportedException($"Formato no soportado: {ext}. Use ZIP o RAR.");
    }

    private static List<(string Nombre, Stream Contenido)> ExtraerXmlsZip(Stream stream)
    {
        var resultado = new List<(string, Stream)>();
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

        foreach (var entrada in zip.Entries)
        {
            if (!entrada.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                continue;

            var ms = new MemoryStream();
            using var s = entrada.Open();
            s.CopyTo(ms);
            ms.Position = 0;
            resultado.Add((Path.GetFileName(entrada.FullName), ms));
        }

        return resultado;
    }

    private static List<(string Nombre, Stream Contenido)> ExtraerXmlsRar(string rutaArchivo)
    {
        var resultado = new List<(string, Stream)>();
        using var rar = RarArchive.OpenArchive(rutaArchivo, new ReaderOptions());

        foreach (var entrada in rar.Entries)
        {
            if (entrada.IsDirectory) continue;
            if (entrada.Key == null || !entrada.Key.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) continue;

            var ms = new MemoryStream();
            using var s = entrada.OpenEntryStream();
            s.CopyTo(ms);
            ms.Position = 0;
            resultado.Add((Path.GetFileName(entrada.Key), ms));
        }

        return resultado;
    }

    public static IEnumerable<string> ObtenerArchivosComprimidos(string carpeta)
        => ExtensionesComprimidas.SelectMany(ext =>
            Directory.GetFiles(carpeta, $"*{ext}", SearchOption.TopDirectoryOnly));
}
