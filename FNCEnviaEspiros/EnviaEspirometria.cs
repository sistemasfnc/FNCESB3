using EventLog;
using FNCEntity;
using FNCFacade;
using FNCUtils;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace FNCEnviaEspiros
{
    /// <summary>
    /// Clase principal encargada de enviar la información de espirometrías.
    /// </summary>
    public class EnviaEspirometria
    {
        /// <summary>
        /// Lista de espirometrías procesadas.
        /// </summary>
        static List<SanitasPrfp> sanitasPrfps = null;

        /// <summary>
        /// Método principal del programa.
        /// </summary>
        static void Main(string[] args)
        {
            // EPPlus 5+ requiere definir el contexto de licencia
            ExcelPackage.License.SetNonCommercialOrganization("Fundacion Neumologica Colombiana");

            sanitasPrfps = new List<SanitasPrfp>();
            GetSanitasPrfp();
            GenerateExcel("Sanitas");

            sanitasPrfps = new List<SanitasPrfp>();
            GetSuraPrfp();
            GenerateExcel("Sura");

            sanitasPrfps = new List<SanitasPrfp>();
            GetEcopetrolPfp();
            GenerateExcel("Ecopetrol");
        }

        /// <summary>
        /// Obtiene la información de espirometrías de Ecopetrol.
        /// </summary>
        static void GetEcopetrolPfp()
        {
            FacadeEspirometria facadeEspirometria = new FacadeEspirometria();
            try
            {
                facadeEspirometria.sconnection = FNCEnviaEspiros.Properties.Settings.Default.SQLConnection;
                facadeEspirometria.sfechainicial = string.IsNullOrEmpty(FNCEnviaEspiros.Properties.Settings.Default.FechaInicial)
                    ? DateTime.Now.ToString("yyyy-MM-dd")
                    : FNCEnviaEspiros.Properties.Settings.Default.FechaInicial;
                facadeEspirometria.sfechafinal = string.IsNullOrEmpty(FNCEnviaEspiros.Properties.Settings.Default.FechaFinal)
                    ? DateTime.Now.ToString("yyyy-MM-dd")
                    : FNCEnviaEspiros.Properties.Settings.Default.FechaFinal;

                sanitasPrfps = facadeEspirometria.GetEcopetrolPrfp();
            }
            catch (Exception ex)
            {
                LogError.WriteError("EnviaEspirometrias", "GetEcopetrolPfp", ex);
                throw;
            }
            finally
            {
                facadeEspirometria = null;
            }
        }

        /// <summary>
        /// Obtiene la información de espirometrías de Sanitas.
        /// </summary>
        static void GetSanitasPrfp()
        {
            FacadeEspirometria facadeEspirometria = new FacadeEspirometria();
            try
            {
                facadeEspirometria.sconnection = FNCEnviaEspiros.Properties.Settings.Default.SQLConnection;
                facadeEspirometria.sfechainicial = string.IsNullOrEmpty(FNCEnviaEspiros.Properties.Settings.Default.FechaInicial)
                    ? DateTime.Now.ToString("yyyy-MM-dd")
                    : FNCEnviaEspiros.Properties.Settings.Default.FechaInicial;
                facadeEspirometria.sfechafinal = string.IsNullOrEmpty(FNCEnviaEspiros.Properties.Settings.Default.FechaFinal)
                    ? DateTime.Now.ToString("yyyy-MM-dd")
                    : FNCEnviaEspiros.Properties.Settings.Default.FechaFinal;

                sanitasPrfps = facadeEspirometria.GetSanitasPrfp(true);
            }
            catch (Exception ex)
            {
                LogError.WriteError("EnviaEspirometrias", "GetSanitasPrfp", ex);
                throw;
            }
            finally
            {
                facadeEspirometria = null;
            }
        }

        /// <summary>
        /// Obtiene la información de espirometrías de Sura.
        /// </summary>
        static void GetSuraPrfp()
        {
            FacadeEspirometria facadeEspirometria = new FacadeEspirometria();
            try
            {
                facadeEspirometria.sconnection = FNCEnviaEspiros.Properties.Settings.Default.SQLConnection;
                facadeEspirometria.sfechainicial = string.IsNullOrEmpty(FNCEnviaEspiros.Properties.Settings.Default.FechaInicial)
                    ? DateTime.Now.ToString("yyyy-MM-dd")
                    : FNCEnviaEspiros.Properties.Settings.Default.FechaInicial;
                facadeEspirometria.sfechafinal = string.IsNullOrEmpty(FNCEnviaEspiros.Properties.Settings.Default.FechaFinal)
                    ? DateTime.Now.ToString("yyyy-MM-dd")
                    : FNCEnviaEspiros.Properties.Settings.Default.FechaFinal;

                sanitasPrfps = facadeEspirometria.GetSuraPrfp();
            }
            catch (Exception ex)
            {
                LogError.WriteError("EnviaEspirometrias", "GetSuraPrfp", ex);
                throw;
            }
            finally
            {
                facadeEspirometria = null;
            }
        }

        /// <summary>
        /// Genera un archivo Excel con la información de las espirometrías.
        /// Usa EPPlus para preservar las fórmulas y estilos de la plantilla.
        /// </summary>
        /// <param name="scompany">Nombre de la compañía para la cual se genera el archivo.</param>
        static void GenerateExcel(string scompany)
        {
            string outputPath = FNCEnviaEspiros.Properties.Settings.Default.ExcelFile
                                + DateTime.Now.ToString("yyyyMMdd") + "_" + scompany + ".xlsx";
            string templatePath = FNCEnviaEspiros.Properties.Settings.Default.ExcelTemplate;

            // Eliminar archivo de salida anterior si existe
            if (File.Exists(outputPath))
            {
                try { File.Delete(outputPath); }
                catch (Exception ex) { LogError.WriteError("EnviaEspirometrias", "GenerateExcel_Delete", ex); }
            }

            // Copiar la plantilla al destino para preservar fórmulas, estilos y hojas auxiliares
            File.Copy(templatePath, outputPath);

            using (var package = new ExcelPackage(new FileInfo(outputPath)))
            {
                var ws = package.Workbook.Worksheets["ESPIROMETRIAS EXTERNOS"];
                if (ws == null)
                    throw new Exception("No se encontró la hoja 'ESPIROMETRIAS EXTERNOS' en la plantilla.");

                int irow = 2; // Fila 1 = encabezado, datos desde fila 2

                foreach (var item in sanitasPrfps)
                {
                    string sresult = GetTestResult(item);
                    string sresultUpper = RemoveDiacritics(sresult).ToUpper();

                    // ── LOG DE DIAGNÓSTICO (eliminar cuando todo funcione correctamente) ──
                    Console.WriteLine($"[{item.sDocumento}] " +
                                      $"PDF encontrado: {!string.IsNullOrEmpty(sresult)} | " +
                                      $"Contiene ESPIROMETR: {sresultUpper.Contains("ESPIROMETR")} | " +
                                      $"Proviene: {item.sProviene}");
                    // ────────────────────────────────────────────────────────────────────

                    // Filtrar solo espirometrías válidas.
                    // Los registros de SentrySuite pueden no tener PDF pero traen sResultado desde la BD.
                    bool esSentrySuite = item.sProviene == "SentrySuite";
                    bool tieneResultadoBD = !string.IsNullOrEmpty(item.sResultado);
                    bool esEspirometria = sresultUpper.Contains("ESPIROMETR");
                    string[] excluir =
                    {
                        "DLCO",
                        "CAPACIDAD FUNCIONAL",
                        "DIFUSION",
                        "PLETISMOGRAFIA",
                        "VOLUMENES",
                        "CPET",
                        "CARDIOPULMONAR",
                        "PRUEBA DE ESFUERZO",
                        "CAPACIDAD DE DIFUSION",
                        "TEST DLCO"
                    };
                    bool esExcluida = excluir.Any(x => sresultUpper.Contains(x));
                    // Si NO contiene ESPIROMETRÍA → descartar
                    if (!esEspirometria && !esSentrySuite && !tieneResultadoBD)
                        continue;
                    // Si contiene palabras de pruebas NO-espirométricas → descartar
                    if (esExcluida)
                        continue;

                    string scomment = !string.IsNullOrEmpty(item.sResultado)
                        ? item.sResultado
                        : GetComment(sresult);

                    // ── LOG DE DIAGNÓSTICO (eliminar cuando todo funcione correctamente) ──
                    Console.WriteLine($"  → scomment: '{scomment}'");
                    // ────────────────────────────────────────────────────────────────────

                    // Col A  — Fecha
                    ws.Cells[irow, 1].Value = item.dtFecha.ToString("dd-MM-yyyy");

                    // Col B  — Código IPS: la plantilla tiene =IFERROR(VLOOKUP(C,UNIVERSO,3,0),"")
                    //          Se omite para preservar la fórmula; se alimenta por col C.

                    // Col C  — Nombre IPS
                    ws.Cells[irow, 3].Value = item.sNomIPS;

                    // Col D  — Descripción tipo documento
                    ws.Cells[irow, 4].Value = item.sTipoDocumento;

                    // Col E  — Tipo documento: la plantilla tiene =IFERROR(VLOOKUP(D,PARAMETROS,2,0),"")
                    //          Se omite para preservar la fórmula; se alimenta por col D.

                    // Col F  — Número de documento
                    ws.Cells[irow, 6].Value = item.sDocumento;

                    // Col G  — Nombres
                    ws.Cells[irow, 7].Value = item.sNombres;

                    // Col H  — Apellidos
                    ws.Cells[irow, 8].Value = item.sApellidos;

                    // Col I  — Edad
                    ws.Cells[irow, 9].Value = item.iEdad;

                    // Col J  — Sexo
                    ws.Cells[irow, 10].Value = item.sGenero;

                    // Col K  — Peso
                    ws.Cells[irow, 11].Value = Convert.ToInt32(item.dPeso);

                    // Col L  — Talla (metros, numérico para que la fórmula IMC funcione)
                    ws.Cells[irow, 12].Value = item.dTalla;

                    // Col M  — IMC: la plantilla tiene =IFERROR(K/(L^2),"")
                    //          Se omite para preservar la fórmula; se alimenta por cols K y L.

                    // Col N  — Diagnóstico
                    ws.Cells[irow, 14].Value = item.sDiagnostico;

                    // Col O  — CIE10: se escribe directamente porque el VLOOKUP de la plantilla
                    //          busca el diagnóstico completo y no coincide con los valores abreviados
                    //          que vienen de la BD (ej: "ASMA" en lugar de "ASMA NO ALERGICA").
                    ws.Cells[irow, 15].Value = item.sCIE10;

                    // Col P  — Factor de riesgo
                    ws.Cells[irow, 16].Value = item.sFactorRiesgo;

                    // Col Q  — Tabaquismo
                    ws.Cells[irow, 17].Value = item.sTabaquismo;

                    // Col R  — Procedimiento
                    ws.Cells[irow, 18].Value = item.sProcedimiento;

                    // Col S  — CVF Pre
                    ws.Cells[irow, 19].Value = item.dCVFPre;

                    // Col T  — VEF1 Pre
                    ws.Cells[irow, 20].Value = item.dVEF1Pre;

                    // Col U  — VEF1/CVF Pre
                    ws.Cells[irow, 21].Value = item.dTasaPre;

                    // Col V  — CVF Post
                    ws.Cells[irow, 22].Value = item.dCVFPos;

                    // Col W  — VEF1 Post
                    ws.Cells[irow, 23].Value = item.dVEF1Post;

                    // Col X  — VEF1/CVF Post
                    ws.Cells[irow, 24].Value = item.dTasaPos;

                    // Col Y  — % de cambio (vacío, calculado externamente o por el operador)
                    ws.Cells[irow, 25].Value = string.Empty;

                    // Col Z  — Resultado / comentario de la interpretación
                    ws.Cells[irow, 26].Value = scomment;

                    // Col AA — Observaciones
                    ws.Cells[irow, 27].Value = item.sObservaciones;

                    irow++;
                }

                package.Save();
            }

            Console.WriteLine($"Excel generado: {outputPath}");
        }

        /// <summary>
        /// Obtiene el texto completo del PDF de espirometría correspondiente al ítem.
        /// </summary>
        static string GetTestResult(SanitasPrfp item)
        {
            string sresult = string.Empty;
            try
            {
                string[] files = Directory.GetFiles(
                    FNCEnviaEspiros.Properties.Settings.Default.PRFPFolder,
                    item.sDocumento + "_" + item.dtFecha.ToString("yyyyMMdd") + "_*.pdf");
                if (files.Length == 0)
                {
                    files = Directory.GetFiles(
                    FNCEnviaEspiros.Properties.Settings.Default.PRFPFolder2,
                    item.sDocumento + "_" + item.dtFecha.ToString("yyyyMMdd") + "_*.pdf");
                }
                foreach (string file in files)
                {
                    sresult = ReadPDFContent(file);
                    if (!string.IsNullOrEmpty(sresult))
                        return sresult;
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("EnviaEspirometrias", "GetTestResult", ex);
            }
            return sresult;
        }

        /// <summary>
        /// Lee y concatena el texto de todas las páginas de un PDF.
        /// </summary>
        static string ReadPDFContent(string filename)
        {
            var sb = new StringBuilder();
            using (PdfReader reader = new PdfReader(filename))
            {
                for (int i = 1; i <= reader.NumberOfPages; i++)
                {
                    var strategy = new LocationTextExtractionStrategy();
                    string pageText = PdfTextExtractor.GetTextFromPage(reader, i, strategy);
                    sb.AppendLine(pageText);
                }
            }
            return sb.ToString().ToUpper();
        }

        /// <summary>
        /// Extrae el bloque de interpretación clínica del texto del PDF.
        /// Soporta interpretaciones de una o varias líneas, con o sin nota clínica al final.
        /// </summary>
        static string GetComment(string sresult)
        {
            try
            {
                // Normalizar saltos de línea
                string text = sresult
                    .Replace("\r\n", "\n")
                    .Replace("\r", "\n");

                // Dividir en líneas y buscar el encabezado de interpretación línea por línea.
                // Este enfoque es robusto frente a espacios invisibles, distintos saltos de línea
                // y variaciones de codificación que iTextSharp puede producir según el PDF.
                string[] allLines = text.Split('\n');

                int interpretacionLineIdx = -1;
                for (int i = 0; i < allLines.Length; i++)
                {
                    string lineUp = RemoveDiacritics(allLines[i]).Trim().ToUpper();

                    // Coincide si la línea ES exactamente el encabezado
                    // o EMPIEZA con él seguido de ":" o espacio
                    if (lineUp == "INTERPRETACION" ||
                        lineUp.StartsWith("INTERPRETACION:") ||
                        lineUp.StartsWith("INTERPRETACION "))
                    {
                        interpretacionLineIdx = i;
                        break;
                    }
                }

                if (interpretacionLineIdx == -1)
                    return string.Empty;

                // Recolectar las líneas de contenido que siguen al encabezado
                var resultLines = new List<string>();

                for (int i = interpretacionLineIdx + 1; i < allLines.Length; i++)
                {
                    string trimmed = allLines[i].Trim();
                    string lineUp = RemoveDiacritics(trimmed).ToUpper();

                    // Detener en línea vacía una vez capturado algo
                    if (string.IsNullOrWhiteSpace(trimmed) && resultLines.Count > 0)
                        break;

                    // Incluir nota clínica y detener
                    if (lineUp.StartsWith("NOTA:") && resultLines.Count > 0)
                    {
                        resultLines.Add(trimmed);
                        break;
                    }

                    // Detener al encontrar secciones posteriores del PDF
                    if (lineUp.StartsWith("DR.") ||
                        lineUp.StartsWith("R.M.") ||
                        lineUp.StartsWith("CFVB") ||
                        lineUp.StartsWith("TENDENCIA") ||
                        lineUp.StartsWith("PRUEBA") ||
                        lineUp.StartsWith("PARAMETROS") ||
                        lineUp.StartsWith("FECHA"))
                        break;

                    if (!string.IsNullOrWhiteSpace(trimmed))
                        resultLines.Add(trimmed);
                }

                return string.Join(" ", resultLines).Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Elimina diacríticos (tildes, etc.) de un texto para facilitar comparaciones,
        /// sin modificar el texto original.
        /// </summary>
        static string RemoveDiacritics(string text)
        {
            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}