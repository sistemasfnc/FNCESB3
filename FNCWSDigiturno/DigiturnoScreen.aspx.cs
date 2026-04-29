using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using FNCEntity;
using EventLog;
using FNCDAC;
using System.Configuration;

namespace FNCWSDigiturno
{
    public partial class DigiturnoScreen : System.Web.UI.Page
    {
        public List<TurnoEntity> lTurno
        {
            get { return (Session["lTurno"] != null) ? Session["lTurno"] as List<TurnoEntity> : new List<TurnoEntity>(); }
            set { Session["lTurno"] = value; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            this.BindGrid();
        }

        private void BindGrid()
        {
            Digiturno oDAC = new Digiturno(ConfigurationManager.ConnectionStrings["SQLConnection"].ToString());
            try
            {
                this.lTurno = oDAC.GetListTurn();
                this.gvTurnos.DataSource = this.lTurno;
                this.gvTurnos.DataBind();
                if (this.lTurno.Count > 0)
                {
                    System.Media.SystemSounds.Asterisk.Play();
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                throw;                
            }
            finally
            {
                oDAC.Dispose();
                oDAC = null;
            }
        }

        protected void Timer1_Tick(object sender, EventArgs e)
        {
            this.BindGrid();
        }
    }
}