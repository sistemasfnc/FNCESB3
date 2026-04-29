using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Xml;
using System.Data;
using System.ComponentModel;
using FNCEntity;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Diagnostics;
using System.Data.SqlTypes;
using System.IO;

namespace FNCUtils
{
    public static class Tools
    {
        public static string SHA256Crypt(string text)
        {
            UTF8Encoding encoder = new UTF8Encoding();
            SHA256Managed sha256hasher = new SHA256Managed();
            byte[] hashedDataBytes = sha256hasher.ComputeHash(encoder.GetBytes(text));
            return byteArrayToString(hashedDataBytes);
        }

        private static string byteArrayToString(byte[] inputArray)
        {
            if (inputArray == null || inputArray.Length == 0)
                throw new ArgumentNullException(nameof(inputArray));
            return string.Concat(inputArray.Select(b => b.ToString("X2")));
        }

        public static XmlElement GetElement(string sXml)
        {
            XmlDocument xDocument = new XmlDocument();
            xDocument.LoadXml(sXml);
            return xDocument.DocumentElement;
        }

        public static string GetResponseHeader()
        {
            return "<RESPONSE>";
        }

        public static string GetResponseFooter()
        {
            return "</RESPONSE>";
        }
        public static bool EsMayorQueHoraEspecificada(int horas, int minutos)
        {
            TimeSpan horaComparacion = new TimeSpan(horas, minutos, 0);
            return DateTime.Now.TimeOfDay > horaComparacion;
        }
        public static string GetSerCod(string sType, string sPlan)
        {
            if (sPlan.Contains("PARTICULAR") && sType.Contains("01"))
            {
                return "30";
            }
            else if (!sPlan.Contains("PARTICULAR") && sType.Contains("01"))
            {
                return "28";
            }
            else if (sPlan.Contains("FCI"))
            {
                return "29";
            }
            else if (!sType.Contains("FNC"))
            {
                return "31";
            }
            else
            {
                return "28";
            }
        }

        public static string GetAgreementType(string sPlan)
        {
            return (sPlan.Contains("PARTICULAR")) ? "P" : "E";
        }


        /// <summary>
        /// Método para obtener el tipo del documento dependiendo del sistema
        /// </summary>
        /// <param name="sType">String tipo de documento</param>
        /// <param name="bIsInspira">Boolean indica si el registro viene desde Inspira</param>
        /// <returns>String tipo de documento convertido</returns>
        public static string GetDocumentType(string sType, bool bIsInspira = true)
        {
            if (bIsInspira)
            {
                switch (sType)
                {
                    case "Registro Civil": return "RC";
                    case "Tarjeta de Identidad": return "TI";
                    case "Cédula de Ciudadanía": return "CC";
                    case "Cédula de Extranjería": return "CE";
                    case "NUIP": return "NU";
                    case "Pasaporte": return "PA";
                    case "NIT": return "NIT";
                    case "Permiso Especial Permanencia": return "PE";
                    case "Menor sin Identificación": return "MS";
                    case "Permiso Temporal": return "PT";
                    default: return "CC";
                }
            }
            else
            {
                switch (sType)
                {
                    case "RC": return "Registro Civil";
                    case "TI": return "Tarjeta de Identidad";
                    case "CC": return "Cédula de Ciudadanía";
                    case "CE": return "Cédula de Extranjería";
                    case "PA": return "Pasaporte";
                    case "NU": return "NUIP";
                    case "PE": return "Permiso Especial Permanencia";
                    case "MS": return "Menor sin Identificación";
                    case "PT": return "Permiso Temporal";
                    default: return "Cédula de Ciudadanía";
                }
            }
        }


        public static string GetDiagnosis(string sGroup)
        {
            if (sGroup.Contains("ASMA"))
            {
                return "J450";
            }
            else if (sGroup.Contains("AIRE"))
            {
                return "J449";
            }
            else if (sGroup.Contains("VASCULA"))
            {
                return "I270";
            }
            return "Z000";
        }

        public static string GetSpeciality(string sService)
        {
            if (sService == "890271" || sService == "890371" || sService == "890372" || sService == "890272" || sService == "890211")
            {
                return "800";
            }
            else
            {
                return "700";
            }
        }

        public static string SubString(string sText, int iLength)
        {
            if (string.IsNullOrEmpty(sText))
                return string.Empty;
            if (iLength < 0)
                throw new ArgumentOutOfRangeException(nameof(iLength));

            return sText.Substring(0, Math.Min(iLength, sText.Length));
        }

        public static string ReplaceChars(string sText)
        {
            if (string.IsNullOrEmpty(sText))
            {
                return string.Empty;
            }

            string pattern = "[';,#?*\\n\\r\"]";         
            return Regex.Replace(sText, pattern, string.Empty).TrimEnd();
        }

        public static bool IsNumeric(string sNumber)
        {
            long iout = 0;
            return long.TryParse(sNumber, out iout);
        }

        public static string GetBranch(string sAdmission)
        {
            if (sAdmission.Contains("-"))
            {
                string[] aAdmission = sAdmission.Split('-');
                return aAdmission[0];
            }
            return "01";
        }

        public static bool IsSpecial(string sPlan, string sGroup)
        {
            return (sPlan.Contains("ASMAIRE") || sPlan.Contains("AIREPOC") || sGroup.Contains("DOMICILI"));
        }

        /// <summary>
        /// Método que convierte una lista genérica en DataTable
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="iList">Lista genérica</param>
        /// <returns>DataTable</returns>
        public static DataTable ToDataTable<T>(this List<T> iList)
        {
            if (iList == null)
                return new DataTable();
            try
            {
                var properties = typeof(T).GetProperties();
                var dataTable = new DataTable();
                dataTable.Columns.AddRange(properties.Select(p => new DataColumn(p.Name, Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType)).ToArray());
                if (iList.Count > 0)
                    dataTable.LoadDataRow(iList.Select(i => properties.Select(p => p.GetValue(i)).ToArray()).ToArray(), true);
                return dataTable;
            }
            catch (Exception ex)
            {
                // log exception
                throw ex;
            }
        }

        /// <summary>
        /// Método para obtener un arreglo de id desde una lista genérica
        /// </summary>
        /// <param name="list">Lista genérica Inspira temporal</param>
        /// <param name="stype">String campo a tener en cuenta</param>
        /// <returns>Arreglo de string con los id</returns>
        public static string[] SerializeObject(List<InspiraTemporal> list, string stype)
        {
            StringBuilder stringBuilder = new StringBuilder();
            string[] aresult = new string[list.Count];
            int i = 0;
            foreach (InspiraTemporal item in list)
            {
                stringBuilder.Append("'");
                if (stype == "code")
                {
                    stringBuilder.Append(item.scod);
                }
                else if (stype == "nit")
                {
                    stringBuilder.Append(item.iparametro3.ToString());
                }
                stringBuilder.Append("'");
                aresult[i] = stringBuilder.ToString();
                i++;
                stringBuilder.Remove(0, stringBuilder.Length);
            }
            stringBuilder = null;
            return aresult;
        }

        /// <summary>
        /// Método para convertir un tipo de objeto en otro teniendo en cuenta que deben tener los mismos parámetros
        /// </summary>
        /// <typeparam name="T">Objeto origen</typeparam>
        /// <param name="o">Objeto destino</param>
        /// <returns>Objeto origen casteado a destino</returns>
        public static T Cast<T>(object o)
        {
            Type sourceType = o.GetType();
            Type targetType = typeof(T);
            object targetObject = Activator.CreateInstance(targetType, null);
            var q = from s in sourceType.GetProperties() from t in targetType.GetProperties() where s.Name == t.Name && t.MemberType == MemberTypes.Property select t;
            foreach (PropertyInfo p in q.ToList())
            {
                targetType.GetProperty(p.Name).SetValue(targetObject, sourceType.GetProperty(p.Name).GetValue(o, null), null);
            }
            return (T)targetObject;
        }

        public static decimal Time2Number(DateTime dateTime)
        {
            return dateTime.Hour + (dateTime.Minute / 100);
        }

        public static string GetMembershipLevel(string slevel)
        {
            switch (slevel)
            {
                case "1": return "A";
                case "2": return "B";
                default: return "C";
            }
        }

        public static bool EqualsAnyOf(this string value, params string[] targets)
        {
            return targets.Any(target => value.Equals(target, StringComparison.Ordinal));
        }

        public static string GetAttentionType(string stype, string sgroup)
        {
            if (!string.IsNullOrEmpty(sgroup))
            {
                if (sgroup.Contains("(TC ") || sgroup == "T")
                {
                    return "T";
                }
                else if (sgroup.Contains("CONSULTA"))
                {
                    return "C";
                }
                else if (sgroup == "H")
                {
                    return "H";
                }
                else if (sgroup == "U" || sgroup == "G")
                {
                    return "U";
                }
                else if (sgroup == "A")
                {
                    return "C";
                }
                else if (sgroup == "V")
                {
                    return "V";
                }
                else if (sgroup == "D")
                {
                    return "D";
                }
            }
            else
            {
                if (stype == "Hospitalizacion")
                {
                    return "H";
                }
                else if (stype == "Investigacion")
                {
                    return "I";
                }
            }
            return "C";
        }

        public static int GetAge(DateTime birthDate)
        {
            int age = DateTime.Now.Year - birthDate.Year;
            if (DateTime.Now.Month < birthDate.Month || (DateTime.Now.Month == birthDate.Month && DateTime.Now.Day < birthDate.Day))
            {
                age--;
            }
            return age;
        }

        public static List<EntryExtended> ResponseToChild(List<EntryResponse> entryResponses)
        {
            List<EntryExtended> lentryExtendeds = new List<EntryExtended>();
            EntryExtended entryExtended = null;
            foreach (var item in entryResponses)
            {
                entryExtended = new EntryExtended()
                {
                    icharge = item.icharge,
                    ientry = item.ientry,
                    ddate = Convert.ToDateTime(item.ddate),
                    sagreement = item.sagreement,
                    iline = item.iline,
                    ipatient = item.ipatient,
                    splan = item.splan,
                    sdocument = item.sdocument,
                    sdocumenttype = item.sdocumenttype,
                    sconcept = item.sconcept,
                    scostcenter = item.scostcenter,
                    sauthorization = item.sauthorization,
                    sunit = item.sunit,
                    srate = item.srate,
                    sservice = item.sservice,
                    sservicename = item.sservicename,
                    iqty = item.iqty,
                    dvalue = (item.dvalue),
                    stemplate = item.stemplate,
                    ientrysource = item.ientrysource,
                };
                lentryExtendeds.Add(entryExtended);
            }
            return lentryExtendeds;
        }

        public static decimal GetDValue(string dvalue)
        {
            // Intenta convertir la cadena a un número
            decimal result;
            if (decimal.TryParse(dvalue, out result))
            {
                return result;
            }
            // Si la conversión no es exitosa, puedes manejar el caso según tus necesidades
            return 0;
        }

        /// <summary>
        /// Método para obtener la información de empresa para crear el cargo si es particular o convenio teniendo en cuenta la tarifa
        /// </summary>
        /// <param name="sagreement">String código del convenio</param>
        /// <param name="sagreementname">String nombre del convenio</param>
        /// <param name="sratename">String nombre de la tarifa</param>
        /// <param name="spatient">String nombre del paciente</param>
        /// <param name="sdocument">String documento del paciente</param>
        /// <returns>Objeto genérico</returns>
        public static Generic GetCompanyFromRate(string sagreement, string sagreementname, string sratename, string spatient, string sdocument)
        {
            Generic generic = new Generic();
            if (sratename.EqualsAnyOf("P", "7", "9"))
            {
                generic.scode = sdocument;
                generic.sname = spatient;
                generic.sfilter = "P";
            }
            else
            {
                generic.scode = sagreement;
                generic.sname = sagreementname;
                generic.sfilter = "E";
            }
            return generic;
        }

        /// <summary>
        /// Método que obtiene el usuario
        /// </summary>
        /// <param name="suser">String nombre del usuario</param>
        /// <returns>String nombre del usuario</returns>
        public static string GetUser(string suser)
        {
            return (string.IsNullOrEmpty(suser) || suser.Contains("Ninguno") || suser.Contains("-.-")) ? "admon" : suser;
        }

        /// <summary>
        /// Método para enviar un JSON vía post a una URL
        /// </summary>
        /// <param name="url">String url destino</param>
        /// <param name="json">String json a enviar</param>
        /// <returns>Boolean verdadero si el envío fue correcto, falso en caso contrario</returns>
        public static bool PostJson(string url, string json)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
                try
                {
                    HttpResponseMessage response =  httpClient.PostAsync(url, content).Result;
                    string responseBody = response.Content.ReadAsStringAsync().Result;                    
                    if (!string.IsNullOrEmpty(responseBody) && response.IsSuccessStatusCode) 
                    {
                        return (responseBody.Contains("01"));
                    }
                    else
                    {
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    EventLog.WriteEntry("InspiraServinte", ex.Message);
                    return false;                    
                }                                
            }
        }

        public static string GetGender(string sGender)
        {
            return (sGender.ToUpper().Contains("HOMBRE") || sGender.ToUpper().Contains("MASC")) ? "M" : "F";
        }

        public static string GetGenderFromInpira(string sGender)
        {
            switch (sGender)
            {
                case "01. Hombre": return "M";
                case "02. Mujer": return "F";
                case "F": return "F";
                default: return "O";
            }
        }

        public static string DecryptParameter(string encryptedValue)
        {
            string key = "0123456789ABCDEF0123456789ABCDEF"; // La misma clave de Salesforce
            string iv = "ABCDEF9876543210"; // El mismo IV de Salesforce

            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] ivBytes = Encoding.UTF8.GetBytes(iv);

            byte[] encryptedBytes = Convert.FromBase64String(encryptedValue);

            using (AesCryptoServiceProvider aes = new AesCryptoServiceProvider())
            {
                aes.Key = keyBytes;
                aes.IV = ivBytes;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using (MemoryStream msDecrypt = new MemoryStream(encryptedBytes))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            return srDecrypt.ReadToEnd(); // Retorna el valor descifrado
                        }
                    }
                }
            }
        }

        public static string EncryptParameter(string plainText)
        {
            string key = "0123456789ABCDEF0123456789ABCDEF"; // La misma clave usada en Apex (32 caracteres)
            string iv = "ABCDEF9876543210"; // El mismo IV usado en Apex (16 caracteres)

            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] ivBytes = Encoding.UTF8.GetBytes(iv);

            byte[] encrypted;

            using (AesCryptoServiceProvider aes = new AesCryptoServiceProvider())
            {
                aes.Key = keyBytes;
                aes.IV = ivBytes;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            // Escribir el texto sin formato en el flujo de cifrado
                            swEncrypt.Write(plainText);
                        }
                        encrypted = msEncrypt.ToArray();
                    }
                }
            }

            // Devolver el resultado cifrado como una cadena codificada en base64
            return Convert.ToBase64String(encrypted);
        }

        public static string GetUserType1306(string safiliation, int iage)
        {
            if ((iage < 18 && safiliation.EqualsAnyOf("1", "2")) || (iage >= 18 && safiliation == "2"))
            {
                return "02";
            }
            else if (safiliation == "P")
            {
                return "12";
            }
            else if (safiliation == "9")
            {
                return "04";
            }
            return "01";
            
        }

        public static string DecryptUrlSafe(string input, string key)
        {
            // Ajustar el tamaño de la clave a 16 bytes (AES-128)
            key = key.PadRight(16, '0').Substring(0, 16);

            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] ivBytes = new byte[16]; // Rellenar con ceros el IV si no estás usando uno

            // Reemplazar los caracteres URL-safe por los originales
            string base64String = input.Replace('-', '+').Replace('_', '/');

            // Añadir el padding de '=' si es necesario
            switch (base64String.Length % 4)
            {
                case 2: base64String += "=="; break;
                case 3: base64String += "="; break;
            }

            // Decodificar el Base64
            byte[] encryptedBytes = Convert.FromBase64String(base64String);

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = keyBytes;
                aesAlg.IV = ivBytes;
                aesAlg.Mode = CipherMode.CBC;
                aesAlg.Padding = PaddingMode.PKCS7;

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msDecrypt = new MemoryStream(encryptedBytes))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt, Encoding.UTF8))  // Añadir UTF-8
                        {
                            // Devolver el texto desencriptado
                            return srDecrypt.ReadToEnd();
                        }
                    }
                }
            }
        }

    }
}
