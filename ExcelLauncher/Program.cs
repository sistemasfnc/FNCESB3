using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Deployment.Application;
using System.Web;
using System.Collections.Specialized;
using System.IO;

namespace ExcelLauncher
{
    class Program
    {

        static void Main(string[] args)
        {
            try
            {
                NameValueCollection nParameters = GetQueryStringParameters();
                string sArguments = (nParameters.Count > 0) ? nParameters[0] : string.Empty;
                Process process = new Process();
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.WorkingDirectory = @"C:\";
                process.StartInfo.FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe");
                process.StartInfo.RedirectStandardInput = true;
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.StandardInput.WriteLine("start excel " + sArguments);                
                process.WaitForExit();
                process.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            
        }

        private static NameValueCollection GetQueryStringParameters()
        {
            NameValueCollection nameValueTable = new NameValueCollection();
            if (ApplicationDeployment.IsNetworkDeployed)
            {
                string queryString = ApplicationDeployment.CurrentDeployment.ActivationUri.Query;
                nameValueTable = HttpUtility.ParseQueryString(queryString);
            }
            return nameValueTable;
        }
    }
}
