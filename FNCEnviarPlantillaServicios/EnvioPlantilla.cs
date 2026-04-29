using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FastExcel;
using FNCDAC;
using System.Data;
using EventLog;
using System.Xml.Linq;
using System.IO;
using FNCUtils;

namespace FNCEnviarPlantillaServicios
{
    internal class EnvioPlantilla
    {
        /// <summary>
        /// Objeto DataTable que almacena la información de la consulta
        /// </summary>
        static DataTable dataTable {  get; set; }

        /// <summary>
        /// Lista genérica de string que almacena los archivos adjuntos del correo electrónico
        /// </summary>
        static List<string> lattachments { get; set; }

        /// <summary>
        /// El programa corre todos los días a las 9 de la mañana, genera la plantilla de envío de servicios a Sanitas y la envía por correo electrónico
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            Console.WriteLine("Inciando el programa...");
            dataTable = new DataTable();
            lattachments = new List<string>();
            GetData();
            GenerateTemplate();
            SendFileMail();
        }

        /// <summary>
        /// Método para obtener la información de los registros de servicios solicitados
        /// </summary>
        static void GetData()
        {
            PlantillaServicios plantillaServicios = new PlantillaServicios();
            try
            {
                //En caso de que las variables de configuración de fecha inicial y fecha final estén vacías, se asigna la fecha del día anterior
                string sinitialdate = (string.IsNullOrEmpty(FNCEnviarPlantillaServicios.Properties.Settings.Default.fechainicial)) ? DateTime.Now.AddDays(-1).ToString("yyyyMMdd") : FNCEnviarPlantillaServicios.Properties.Settings.Default.fechainicial;
                string sfinaldate = (string.IsNullOrEmpty(FNCEnviarPlantillaServicios.Properties.Settings.Default.fechafinal)) ? DateTime.Now.AddDays(-1).ToString("yyyyMMdd") : FNCEnviarPlantillaServicios.Properties.Settings.Default.fechafinal;
                //Se asigna la cadena de conexión a la base de datos FNCStats en el servidor SQL Server HEIMDALL
                plantillaServicios.sconnection = FNCEnviarPlantillaServicios.Properties.Settings.Default.fncstats;
                //Se obtiene la información de los servicios ordenados para sanitas desde el componente de acceso a la BD. Para este caso me salté el uso de la fachada para poder terminar rápido el desarrollo (mala práctica)
                Console.WriteLine("Obteniendo datos");
                dataTable = plantillaServicios.GetSanitasServiceTemplate(sinitialdate, sfinaldate);
            }
            catch (Exception ex)
            {
                //En caso de excepción se escribe el error en el Event Viewer de Windows en la aplicación EnvioPlantilla fuente EnvioPlantilla
                LogError.WriteError("EnvioPlantilla", "EnvioPlantilla", ex);
            }            
        }

        /// <summary>
        /// Método que genera la información de la plantilla en Excel con base en la información obtenida en el DataTable y con base en la plantilla general enviada por Sanitas
        /// </summary>
        static void GenerateTemplate()
        {
            var worksheet = new Worksheet();
            var cells = new List<Cell>();
            var rows = new List<Row>();
            //Se obtiene el encabezado de la plantilla
            cells.AddRange(GetFileHeader());
            rows.Add(new Row(1, cells));
            Cell[] acells = null;
            int irow = 2;
            //Se recorren los registros del DataTable para generar la información de las celdas del archivo (para más información de las celdas del archivo abrir la plantilla)
            foreach (DataRow item in dataTable.Rows)
            {
                //Se crean las celdas que se van a escribir en la fila
                acells = new Cell[]
                {
                    new Cell(1, item["Fecha_Envio"].ToString()),
                    new Cell(2, item["Nit_Prestador_Remitente"].ToString()),
                    new Cell(3, item["Codigo_Habilitacion"].ToString()),
                    new Cell(4, item["Nombre_Prestador_Remitente"].ToString()),
                    new Cell(5, item["Tipo_Identificacion_del_Afiliado"].ToString()),
                    new Cell(6, item["Numero_de_Identificación_del_Afiliado"].ToString()),
                    new Cell(7, item["Nombre_Paciente"].ToString()),
                    new Cell(8, Tools.SubString(item["Telefono_Celular_1"].ToString(), 10)),
                    new Cell(9, Tools.SubString(item["Telefono_Celular_2"].ToString(), 10)),
                    new Cell(10, item["Fecha_Atencion"].ToString()),
                    new Cell(11, item["CIE10"].ToString()),
                    new Cell(12, item["Nombre_Medico"].ToString()),
                    new Cell(13, item["Codigo_Especialidad_Remitente"].ToString()),
                    new Cell(14, item["Especialidad_Remitente"].ToString()),
                    new Cell(15, Tools.ReplaceChars(item["Codigo_CUPS_Prestacion"].ToString()).Trim('-')),
                    new Cell(16, item["Descripcion_Prestacion"].ToString()),
                    new Cell(17, item["Cantidad"].ToString()),
                    new Cell(18, item["Justificacion_Clinica"].ToString()),
                    new Cell(19, string.Empty),
                    new Cell(20, string.Empty),
                    new Cell(21, string.Empty),
                    new Cell(22, string.Empty),
                    new Cell(23, string.Empty),
                    new Cell(24, string.Empty),
                    new Cell(25, string.Empty)
                };
                //Se agrega la nueva fila y se aumenta el contador
                rows.Add(new Row(irow, acells.ToArray()));
                irow++;
            }
            //Se asignan las filas a la hoja de Excel
            worksheet.Rows = rows;
            //Se obtiene la información del archivo de Excel que se va a generar. Se le agrega al archivo la fecha para diferenciarlo
            var outputFile = new FileInfo(FNCEnviarPlantillaServicios.Properties.Settings.Default.ExcelFile + DateTime.Now.ToString("yyyyMMdd") + ".xlsx");
            //Se obtiene la información de la plantilla base recibida por Sanitas
            var templateFile = new FileInfo(FNCEnviarPlantillaServicios.Properties.Settings.Default.plantilla);
            //Si el archivo de salida de excel existe, se elimina
            if (File.Exists(outputFile.FullName))
            {
                try
                {
                    File.Delete(outputFile.FullName);
                }
                catch (Exception ex)
                {
                    //En caso de excepción se escribe el error en el Event Viewer de Windows en la aplicación EnvioPlantilla fuente EnvioPlantilla
                    LogError.WriteError("EnvioPlantilla", "EnvioPlantilla", ex);
                }
            }
            //Se copia la información generada en el archivo de Excel tomando como base la plantilla enviada por Sanitas
            using (var fastExcel = new FastExcel.FastExcel(templateFile, outputFile))
            {
                fastExcel.Write(worksheet, "PLANILLA");
                //Se agrega el archivo generado a la lista de archivos adjuntos a enviar por correo
                lattachments.Add(outputFile.FullName);
            }
        }

        /// <summary>
        /// Método que genera la fila de encabezado del archivo de Excel a enviar
        /// </summary>
        /// <returns>Lista genérica de celdas para agregar el archivo, contiene las columnas a generar</returns>
        static List<Cell> GetFileHeader()
        {
            Cell[] cells = new Cell[]
            {
                new Cell(1, "Fecha_Envio"),
                new Cell(2, "Nit_Prestador_remitente"),
                new Cell(3, "Codigo_Sucursal_BH"),
                new Cell(4, "Nombre_Prestador_Remitente"),
                new Cell(5, "Tipo_Identificacion_del_Afiliado"),
                new Cell(6, "Numero_de_Identificacion_del_Afiliado"),
                new Cell(7, "Nombre_Paciente"),
                new Cell(8, "Telefono_Celular_1"),
                new Cell(9, "Telefono_Celular_2"),
                new Cell(10, "Fecha_Atencion"),
                new Cell(11, "CIE10"),
                new Cell(12, "Nombre_Medico"),
                new Cell(13, "Codigo_Especialidad_Remitente"),
                new Cell(14, "Especialidad_Remitente"),
                new Cell(15, "Codigo_CUPS_Prestacion"),
                new Cell(16, "Descripcion_Prestacion"),
                new Cell(17, "Cantidad"),
                new Cell(18, "Justificacion_Clinica"),
                new Cell(19, "Edad_Gestacional"),
                new Cell(20, "Anestesia"),
                new Cell(21, "Sedación"),
                new Cell(22, "Contraste"),
                new Cell(23, "Comparativo"),
                new Cell(24, "Bilateral"),
                new Cell(25, "No_Aplica")
            };
            return cells.ToList();
        }

        /// <summary>
        /// Método para realizar el envío del archivo de Excel generado por correo electrónico
        /// </summary>
        static void SendFileMail()
        {
            //Se leen los destinatarios del archivo de la configuración del programa. Tener en cuenta que para agregar un destinatario adicional, esta variable debe ir separada por comas
            string[] arecipients = FNCEnviarPlantillaServicios.Properties.Settings.Default.mailrecipients.Split(',');
            //Se crea la instancia del objeto de envío de correo electrónico con la información requerida
            SendMail sendMail = new SendMail()
            {
                sUser = FNCEnviarPlantillaServicios.Properties.Settings.Default.mailuser,
                sPassword = FNCEnviarPlantillaServicios.Properties.Settings.Default.mailpassword,
                sServer = FNCEnviarPlantillaServicios.Properties.Settings.Default.mailserver,
                iPort = FNCEnviarPlantillaServicios.Properties.Settings.Default.mailport,
                attachments = lattachments,
                lRecipient = arecipients.ToList(),
                sContentType = "text/plain",
                sSubject = FNCEnviarPlantillaServicios.Properties.Settings.Default.mailsubject + " " + DateTime.Now.ToShortDateString(),
                sMessage = FNCEnviarPlantillaServicios.Properties.Settings.Default.mailbody,
                bisTLS = false,
                sFrom = FNCEnviarPlantillaServicios.Properties.Settings.Default.mailfrom,
            };
            try
            {
                //Se realiza la invocación del método de envío del correo, se envía el parámetro true para que tenga en cuenta la lista de adjuntos
                sendMail.Send(true);
            }
            catch (Exception ex)
            {
                //En caso de excepción se escribe el error en el Event Viewer de Windows en la aplicación EnvioPlantilla fuente EnvioPlantilla
                LogError.WriteError("EnvioPlantilla", "EnvioPlantilla", ex);

            }            
        }
    }    
}
