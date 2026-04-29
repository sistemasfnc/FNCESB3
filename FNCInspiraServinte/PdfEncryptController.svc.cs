using FNCInspiraServinte;
using System;
using System.IO;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using iText.Kernel.Pdf;
using iText.Kernel.Exceptions;
using System.Configuration;
using EventLog;
using System.ServiceModel.Web;

namespace FNCInspiraServinte
{
    /// <summary>
    /// Clase encargada de la gestión y cifrado de archivos PDF.
    /// </summary>
    public class PdfEncryptController : IPdfEncryptController
    {
        /// <summary>
        /// Cifra un archivo PDF utilizando una contraseña proporcionada y retorna el PDF cifrado en formato base64.
        /// </summary>
        /// <param name="pdfBase64">El archivo PDF en formato base64.</param>
        /// <param name="password">La contraseña utilizada para cifrar el PDF.</param>
        /// <param name="ishistory">Indica si el PDF debe ser guardado en el historial antes de ser cifrado.</param>
        /// <returns>El archivo PDF cifrado en formato base64.</returns>
        /// <exception cref="FaultException">Lanza una excepción en caso de errores durante el proceso de cifrado.</exception>
        public string EncryptPdf(string pdfBase64, string password, bool ishistory)
        {
            try
            {
                // Convertir el archivo PDF de base64 a un arreglo de bytes.
                byte[] pdfBytes = Convert.FromBase64String(pdfBase64);

                // Si ishistory es verdadero, guarda el PDF antes de cifrarlo.
                if (ishistory)
                {
                    this.SavePDF(pdfBytes, password);
                }

                // Usar MemoryStream para manejar la entrada y salida de datos en memoria.
                using (MemoryStream inputStream = new MemoryStream(pdfBytes))
                using (MemoryStream outputStream = new MemoryStream())
                {
                    // Configurar el cifrado del PDF utilizando iText.
                    PdfWriter writer = new PdfWriter(outputStream, new WriterProperties()
                        .SetStandardEncryption(
                            Encoding.UTF8.GetBytes(password), // Contraseña de usuario
                            Encoding.UTF8.GetBytes(password), // Contraseña de propietario (en este caso, la misma)
                            EncryptionConstants.ALLOW_PRINTING, // Permisos del PDF
                            EncryptionConstants.ENCRYPTION_AES_128 | EncryptionConstants.DO_NOT_ENCRYPT_METADATA // Algoritmo de cifrado y configuración
                        )
                    );

                    // Leer y escribir el documento PDF para aplicar el cifrado.
                    using (PdfDocument pdfDoc = new PdfDocument(new PdfReader(inputStream), writer))
                    {
                        /*try
                        {
                            if (pdfDoc.GetNumberOfPages() > 1 && !ishistory)
                            {
                                pdfDoc.RemovePage(pdfDoc.GetLastPage());
                            }
                        }
                        catch (Exception ex)
                        {
                            LogError.WriteError("PdfCrypt", "PdfCrypt", ex);
                        }      */
                        pdfDoc.Close(); // Cerrar el documento para finalizar el cifrado.
                    }

                    // Convertir el archivo PDF cifrado de bytes a base64.
                    byte[] encryptedPdfBytes = outputStream.ToArray();
                    return Convert.ToBase64String(encryptedPdfBytes);
                }
            }
            catch (PdfException pdfEx)
            {
                // Manejar errores específicos de iText.
                string errorMessage = $"Error de iText al cifrar el PDF: {pdfEx.Message}";
                throw new FaultException(errorMessage);
            }
            catch (Exception ex)
            {
                // Registrar cualquier otro error que ocurra.
                LogError.WriteError("PdfCrypt", "PdfCrypt", ex);
                throw;
            }
        }

        public Stream EncryptPdfBlob(Stream pdfStream)
        {
            try
            {
                // Obtener la contraseña y el valor de isHistory desde los encabezados
                string password = WebOperationContext.Current.IncomingRequest.Headers["password"];
                bool isHistory = bool.Parse(WebOperationContext.Current.IncomingRequest.Headers["isHistory"]);

                // Leer los bytes desde el flujo de entrada
                byte[] pdfBytes;
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    pdfStream.CopyTo(memoryStream);
                    pdfBytes = memoryStream.ToArray();
                }

                // Guardar el PDF si es necesario
                if (isHistory)
                {
                    SavePDF(pdfBytes, password);
                }

                // Cifrar el PDF
                byte[] encryptedPdfBytes;
                using (MemoryStream inputStream = new MemoryStream(pdfBytes))
                using (MemoryStream outputStream = new MemoryStream())
                {
                    PdfWriter writer = new PdfWriter(outputStream, new WriterProperties()
                        .SetStandardEncryption(
                            Encoding.UTF8.GetBytes(password),
                            Encoding.UTF8.GetBytes(password),
                            EncryptionConstants.ALLOW_PRINTING,
                            EncryptionConstants.ENCRYPTION_AES_128 | EncryptionConstants.DO_NOT_ENCRYPT_METADATA
                        )
                    );

                    using (PdfDocument pdfDoc = new PdfDocument(new PdfReader(inputStream), writer))
                    {
                        pdfDoc.Close();
                    }

                    encryptedPdfBytes = outputStream.ToArray();
                }

                // Retornar el PDF cifrado como un flujo de bytes
                WebOperationContext.Current.OutgoingResponse.ContentType = "application/octet-stream";
                return new MemoryStream(encryptedPdfBytes);
            }
            catch (Exception ex)
            {
                LogError.WriteError("PdfCrypt", "PdfCrypt", ex);
                throw new FaultException("Error al cifrar el PDF: " + ex.Message);
            }
        }


        /// <summary>
        /// Guarda el archivo PDF en un directorio específico definido en el archivo de configuración.
        /// </summary>
        /// <param name="pdfBytes">El archivo PDF en formato de arreglo de bytes.</param>
        /// <param name="patient">El nombre del paciente o identificador utilizado para nombrar el archivo.</param>
        private void SavePDF(byte[] pdfBytes, string patient)
        {
            try
            {
                // Obtener la ruta del directorio desde el archivo de configuración.
                string directory = ConfigurationManager.AppSettings["PdfDir"].ToString();

                // Verificar si el directorio base existe, si no, crearlo.
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Combinar el directorio base con el nombre del paciente.
                directory = Path.Combine(directory, patient);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Generar el nombre del archivo con el nombre del paciente y la fecha actual.
                string fileName = patient + "_" + DateTime.Now.ToString("yyyyMMdd") + ".PDF";
                string filePath = Path.Combine(directory, fileName);

                // Verificar si el archivo ya existe, si no, crearlo o sobrescribirlo.
                if (!File.Exists(filePath))
                {
                    File.WriteAllBytes(filePath, pdfBytes);
                }
                else
                {
                    // Si el archivo ya existe, sobrescribir el archivo existente.
                    File.WriteAllBytes(filePath, pdfBytes);
                }
            }
            catch (Exception ex)
            {
                // Registrar cualquier error que ocurra al guardar el archivo PDF.
                LogError.WriteError("PdfCrypt", "PdfCrypt", ex);
            }
        }
    }
}
