using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClientesNeum;
using System.Xml;
using FNCEntity;

namespace FNCDigiturno
{
    public class ESBDigiturno : IDisposable
    {
        private ITCIELSelectorVirtual45 oSelector { get; set; }
        
        public ESBDigiturno()
        {
            this.oSelector = new TCIELSelectorVirtual45();
        }

        private bool CreateConnection(DigiturnoConnection oDigiturno)
        {
            try
            {                
                return this.oSelector.SolicitaConexion(oDigiturno.CodigoSelector, oDigiturno.CodigoUsuario, oDigiturno.ClaveUsuario, oDigiturno.HostServidor, oDigiturno.Puerto);
            }
            catch (Exception)
            {
                return false;
            }            
        }

        public bool CloseConnection()
        {
            try
            {
                return this.oSelector.CerrarConexion();
            }
            catch (Exception)
            {
                return false;
            }
        }

        public TurnResult GenerateTurn(DigiturnoConnection oDigiturno, TurnoEntity oEntity)
        {           
            TCIELNuevoTurno45 oTurn = null;            
            //if (this.CreateConnection(oDigiturno))   
            if (true)
            {
                try
                {
                    oTurn = this.GetTurnClass(oEntity);                      
                    if (this.oSelector.GenerarTurno(oTurn))
                    {
                        return new TurnResult()
                        {
                            turncode = oTurn.strCodTurno,
                            turnnumber = oTurn.strNumTurno,
                        };
                    }
                    else
                    {
                        return new TurnResult()
                        {
                            errordescription = this.oSelector.strUltimoError,
                            errorcode = "04"
                        };
                    }

                }
                catch (Exception)
                {
                    return new TurnResult()
                    {
                        errordescription = this.oSelector.strUltimoError,
                        errorcode = "04"
                    };
                }
                finally
                {
                    this.CloseConnection();
                }
            }
            /*else
            {                
                return new TurnResult() { errordescription = this.oSelector.strUltimoError, errorcode = "02" };
            } */           
        }

        private TCIELNuevoTurno45 GetTurnClass(TurnoEntity oEntity)
        {
            TCIELNuevoTurno45 oTurn = this.oSelector.CreaObjetoNuevoTurno();
            //oTurn.strCodCola = "{" + oEntity.queuecode.ToUpper() + "}";
            oTurn.strCodCola = "{C134CCC8-6BC3-4630-A5DF-A4F91F30B469}";
            oTurn.bolConteo = oEntity.counting;
            oTurn.bolNoImprimir = oEntity.notprint;
            //oTurn.strCodPrioridad = "{" + oEntity.prioritycode.ToUpper() + "}";
            oTurn.strCodPrioridad = "{BD8EF320-FC16-428B-A9E3-5B68B070D5E4}";
            oTurn.strCodSalaDestino = oEntity.roomcode;
            oTurn.strNomCliente = oEntity.patientname;
            oTurn.strIDCliente = oEntity.patientid;
            oTurn.strObservaciones = oEntity.observations;
            oTurn.strCodCliente = oEntity.patientcode;
            oTurn.intCopias = 1;            
            return oTurn;
        }

        
        public void Dispose()
        {            
            this.oSelector = null;            
            GC.SuppressFinalize(this);
            GC.Collect();            
        }        
    }
}
