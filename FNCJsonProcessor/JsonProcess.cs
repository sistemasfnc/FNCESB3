using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FNCJsonProcessor
{
    internal class JsonProcess
    {
        private static readonly string RootPath = FNCJsonProcessor.Properties.Settings.Default.RipsPath;
        private static readonly string RipsPath = FNCJsonProcessor.Properties.Settings.Default.RipsSource;
        private static readonly string ProcessedFilesLog = FNCJsonProcessor.Properties.Settings.Default.LogFile;
        private static readonly string InvoicesValidationFile = FNCJsonProcessor.Properties.Settings.Default.InvoicesFile;
        private static readonly HashSet<string> ProcessedFiles = new HashSet<string>();
        private static readonly HashSet<string> InvoiceList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static void Main(string[] args)
        {
            LoadProcessedFiles();
            LoadInvoiceList();

            Console.WriteLine($"Iniciando procesamiento de archivos JSON en {RootPath}...");
            Console.WriteLine($"Buscando archivos modificados entre {DateTime.Now.AddDays(-1):yyyy-MM-dd} y {DateTime.Now:yyyy-MM-dd}");
            ProcessDirectory(RootPath);

            LoadProcessedFiles();

            Console.WriteLine($"Iniciando procesamiento de archivos JSON en {RipsPath}...");
            Console.WriteLine($"Buscando archivos modificados entre {DateTime.Now.AddDays(-1):yyyy-MM-dd} y {DateTime.Now:yyyy-MM-dd}");
            ProcessDirectory(RipsPath);

            Console.WriteLine("Procesamiento completado.");
        }

        private static void LoadProcessedFiles()
        {
            if (File.Exists(ProcessedFilesLog))
            {
                var lines = File.ReadAllLines(ProcessedFilesLog);
                foreach (var line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        ProcessedFiles.Add(line.Trim());
                    }
                }
            }
        }

        private static void LoadInvoiceList()
        {
            try
            {
                if (File.Exists(InvoicesValidationFile))
                {
                    var lines = File.ReadAllLines(InvoicesValidationFile);
                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            InvoiceList.Add(line.Trim());
                        }
                    }
                    Console.WriteLine($"Cargadas {InvoiceList.Count} facturas para validación desde {InvoicesValidationFile}");
                }
                else
                {
                    Console.WriteLine($"⚠ No se encontró el archivo de validación de facturas: {InvoicesValidationFile}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cargando facturas desde {InvoicesValidationFile}: {ex.Message}");
            }
        }

        private static void ProcessDirectory(string directory)
        {
            try
            {
                foreach (var file in Directory.GetFiles(directory, "*.json"))
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        var lastWriteTime = fileInfo.LastWriteTime.Date;
                        var yesterday = DateTime.Now.AddDays(-10).Date;
                        var today = DateTime.Now.Date;

                        if (lastWriteTime >= yesterday && lastWriteTime <= today)
                        {
                            if (ProcessedFiles.Contains(file))
                            {
                                Console.WriteLine($"Archivo ya procesado: {file}");
                                continue;
                            }

                            ProcessJsonFile(file);
                            LogProcessedFile(file);
                        }
                        else
                        {
                            Console.WriteLine($"Archivo fuera del rango de fechas: {file} (Última modificación: {lastWriteTime:yyyy-MM-dd})");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error al verificar archivo {file}: {ex.Message}");
                    }
                }

                foreach (var subDir in Directory.GetDirectories(directory))
                {
                    ProcessDirectory(subDir);
                }
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine($"No se tiene acceso al directorio: {directory}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al procesar directorio {directory}: {ex.Message}");
            }
        }

        private static void ProcessJsonFile(string filePath)
        {
            try
            {
                Console.WriteLine($"Procesando archivo: {filePath}");

                var jsonString = File.ReadAllText(filePath);
                var jsonDoc = JsonDocument.Parse(jsonString, new JsonDocumentOptions { AllowTrailingCommas = true });

                string facturaActual = null;
                bool facturaEnLista = false;

                // Primero obtener el numFactura para verificar si está en la lista
                if (jsonDoc.RootElement.TryGetProperty("numFactura", out JsonElement facturaElement))
                {
                    facturaActual = facturaElement.GetString();
                    if (!facturaActual.StartsWith("SETT"))
                        facturaActual = "SETT" + facturaActual;

                    facturaEnLista = InvoiceList.Contains(facturaActual);
                }

                using (var stream = new MemoryStream())
                {
                    using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                    {
                        writer.WriteStartObject();
                        bool modified = false;

                        foreach (var element in jsonDoc.RootElement.EnumerateObject())
                        {
                            if (element.Name == "numFactura")
                            {
                                string currentValue = element.Value.GetString();
                                if (!currentValue.StartsWith("SETT"))
                                {
                                    string newValue = "SETT" + currentValue;
                                    writer.WriteString("numFactura", newValue);
                                    modified = true;
                                    Console.WriteLine($"Modificado numFactura: {currentValue} -> {newValue}");
                                }
                                else
                                {
                                    writer.WriteString(element.Name, element.Value.GetString());
                                }
                            }
                            else if (element.Name == "usuarios")
                            {
                                writer.WritePropertyName(element.Name);
                                writer.WriteStartArray();

                                foreach (var usuario in element.Value.EnumerateArray())
                                {
                                    writer.WriteStartObject();

                                    foreach (var usuarioProp in usuario.EnumerateObject())
                                    {
                                        if (usuarioProp.Name == "tipoUsuario" && facturaEnLista)
                                        {
                                            string tipoActual = usuarioProp.Value.GetString();
                                            if (tipoActual == "01")
                                            {
                                                writer.WriteString("tipoUsuario", "11");
                                                modified = true;
                                                Console.WriteLine($"Modificado tipoUsuario: {tipoActual} -> 11 (Factura: {facturaActual})");
                                            }
                                            else
                                            {
                                                writer.WriteString("tipoUsuario", tipoActual);
                                            }
                                        }
                                        else if (usuarioProp.Name == "servicios")
                                        {
                                            writer.WritePropertyName(usuarioProp.Name);
                                            writer.WriteStartObject();

                                            foreach (var serviciosProp in usuarioProp.Value.EnumerateObject())
                                            {
                                                if (serviciosProp.Name == "procedimientos")
                                                {
                                                    writer.WritePropertyName(serviciosProp.Name);
                                                    writer.WriteStartArray();

                                                    foreach (var procedimiento in serviciosProp.Value.EnumerateArray())
                                                    {
                                                        writer.WriteStartObject();

                                                        foreach (var procProp in procedimiento.EnumerateObject())
                                                        {
                                                            if (procProp.Name == "finalidadTecnologiaSalud" && procProp.Value.GetString() != "15")
                                                            {
                                                                writer.WriteString("finalidadTecnologiaSalud", "15");
                                                                modified = true;
                                                                Console.WriteLine("Modificado finalidadTecnologiaSalud a 15");
                                                            }
                                                            else
                                                            {
                                                                procProp.WriteTo(writer);
                                                            }
                                                        }

                                                        writer.WriteEndObject();
                                                    }

                                                    writer.WriteEndArray();
                                                }
                                                else
                                                {
                                                    serviciosProp.WriteTo(writer);
                                                }
                                            }

                                            writer.WriteEndObject();
                                        }
                                        else
                                        {
                                            usuarioProp.WriteTo(writer);
                                        }
                                    }

                                    writer.WriteEndObject();
                                }

                                writer.WriteEndArray();
                            }
                            else
                            {
                                element.WriteTo(writer);
                            }
                        }

                        writer.WriteEndObject();

                        if (modified)
                        {
                            writer.Flush();
                            File.WriteAllBytes(filePath, stream.ToArray());
                            Console.WriteLine($"Archivo modificado y guardado: {filePath}");
                        }
                        else
                        {
                            Console.WriteLine($"No se requirieron modificaciones en: {filePath}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al procesar archivo {filePath}: {ex.Message}");
            }
        }

        private static void LogProcessedFile(string filePath)
        {
            try
            {
                ProcessedFiles.Add(filePath);
                File.AppendAllText(ProcessedFilesLog, filePath + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al registrar archivo procesado: {ex.Message}");
            }
        }
    }
}
