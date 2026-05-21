using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FacturasFacil.Core.Services;

namespace FacturasFacil.Desktop.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private string _carpetaOrigen = string.Empty;
    private string _carpetaDestino = string.Empty;
    private string _estado = string.Empty;
    private bool _procesando;
    private bool _exitoso;
    private string _rutaExcelGenerado = string.Empty;

    public string CarpetaOrigen
    {
        get => _carpetaOrigen;
        set { _carpetaOrigen = value; OnPropertyChanged(); ActualizarComandos(); }
    }

    public string CarpetaDestino
    {
        get => _carpetaDestino;
        set { _carpetaDestino = value; OnPropertyChanged(); ActualizarComandos(); }
    }

    public string Estado
    {
        get => _estado;
        set { _estado = value; OnPropertyChanged(); }
    }

    public bool Procesando
    {
        get => _procesando;
        set { _procesando = value; OnPropertyChanged(); ActualizarComandos(); }
    }

    public bool Exitoso
    {
        get => _exitoso;
        set { _exitoso = value; OnPropertyChanged(); }
    }

    public string RutaExcelGenerado
    {
        get => _rutaExcelGenerado;
        set { _rutaExcelGenerado = value; OnPropertyChanged(); }
    }

    public ObservableCollection<string> Errores { get; } = [];
    public ObservableCollection<LogEntry> Log { get; } = [];

    public ICommand SeleccionarOrigenCmd { get; }
    public ICommand SeleccionarDestinoCmd { get; }
    public ICommand ProcesarCmd { get; }
    public ICommand AbrirExcelCmd { get; }
    public ICommand AbrirCarpetaDestinoCmd { get; }

    public MainViewModel()
    {
        SeleccionarOrigenCmd  = new RelayCommand(_ => SeleccionarCarpetaOrigen());
        SeleccionarDestinoCmd = new RelayCommand(_ => SeleccionarCarpetaDestino());
        ProcesarCmd           = new RelayCommand(_ => ProcesarAsync(), _ => PuedeProcesar());
        AbrirExcelCmd         = new RelayCommand(_ => AbrirArchivo(RutaExcelGenerado), _ => !string.IsNullOrEmpty(RutaExcelGenerado));
        AbrirCarpetaDestinoCmd = new RelayCommand(_ => AbrirCarpeta(CarpetaDestino), _ => Directory.Exists(CarpetaDestino));

        // Valores por defecto basados en la carpeta del proyecto
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var raiz = EncontrarRaizProyecto(baseDir);
        if (raiz != null)
        {
            CarpetaOrigen  = Path.Combine(raiz, "Facturas");
            CarpetaDestino = Path.Combine(raiz, "ResultadoExcel");
        }
    }

    private bool PuedeProcesar() =>
        !Procesando &&
        !string.IsNullOrWhiteSpace(CarpetaOrigen) &&
        Directory.Exists(CarpetaOrigen) &&
        !string.IsNullOrWhiteSpace(CarpetaDestino);

    private void SeleccionarCarpetaOrigen()
    {
        var carpeta = MostrarDialogoCarpeta("Seleccionar carpeta con archivos ZIP/RAR", CarpetaOrigen);
        if (carpeta != null) CarpetaOrigen = carpeta;
    }

    private void SeleccionarCarpetaDestino()
    {
        var carpeta = MostrarDialogoCarpeta("Seleccionar carpeta de destino para el Excel", CarpetaDestino);
        if (carpeta != null) CarpetaDestino = carpeta;
    }

    private async void ProcesarAsync()
    {
        Procesando = true;
        Exitoso = false;
        RutaExcelGenerado = string.Empty;
        Errores.Clear();
        Log.Clear();
        Estado = "Procesando facturas...";

        AgregarLog("Iniciando procesamiento...", LogTipo.Info);

        try
        {
            var resultado = await Task.Run(() =>
                FacturaProcessor.ProcesarCarpeta(CarpetaOrigen, CarpetaDestino));

            AgregarLog($"Archivos procesados: {resultado.TotalArchivos}", LogTipo.Info);
            AgregarLog($"Facturas extraídas: {resultado.Facturas.Count}", LogTipo.Info);

            if (resultado.Errores.Count > 0)
            {
                foreach (var err in resultado.Errores)
                {
                    Errores.Add($"{err.Archivo}: {err.Mensaje}");
                    AgregarLog($"Error en {err.Archivo}: {err.Mensaje}", LogTipo.Error);
                }
            }

            if (resultado.RutaExcel != null)
            {
                RutaExcelGenerado = resultado.RutaExcel;
                Exitoso = true;
                Estado = $"Excel generado: {Path.GetFileName(resultado.RutaExcel)}";
                AgregarLog($"Excel guardado en: {resultado.RutaExcel}", LogTipo.Exito);
            }
            else
            {
                Estado = "No se encontraron facturas válidas en los archivos.";
                AgregarLog("No se generó Excel: no hay facturas válidas.", LogTipo.Advertencia);
            }
        }
        catch (Exception ex)
        {
            Estado = $"Error: {ex.Message}";
            AgregarLog($"Error inesperado: {ex.Message}", LogTipo.Error);
        }
        finally
        {
            Procesando = false;
        }
    }

    private void AgregarLog(string mensaje, LogTipo tipo) =>
        Log.Add(new LogEntry(DateTime.Now, mensaje, tipo));

    private static string? MostrarDialogoCarpeta(string titulo, string carpetaInicial)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = titulo };
        if (Directory.Exists(carpetaInicial))
            dialog.InitialDirectory = carpetaInicial;

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    private static void AbrirArchivo(string ruta)
    {
        if (File.Exists(ruta))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ruta) { UseShellExecute = true });
    }

    private static void AbrirCarpeta(string ruta)
    {
        if (Directory.Exists(ruta))
            System.Diagnostics.Process.Start("explorer.exe", ruta);
    }

    private static string? EncontrarRaizProyecto(string desde)
    {
        var dir = new DirectoryInfo(desde);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Facturas")) ||
                File.Exists(Path.Combine(dir.FullName, "FacturasFacil.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    private void ActualizarComandos()
    {
        (ProcesarCmd as RelayCommand)?.RaiseCanExecuteChanged();
        (AbrirCarpetaDestinoCmd as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public record LogEntry(DateTime Hora, string Mensaje, LogTipo Tipo);

public enum LogTipo { Info, Exito, Advertencia, Error }

public class RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? p) => canExecute?.Invoke(p) ?? true;
    public void Execute(object? p) => execute(p);
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
