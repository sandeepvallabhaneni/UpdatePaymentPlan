using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualBasic.FileIO;
using System.ServiceModel;
using System.Diagnostics;

namespace UpdateFinancialPaymentPlan
{
    class Program
    {
        private static string DropFolder, ArchiveFolder, FNPattern, DeliverTo;
        private static int ColNum, MaxErrs;
        private static string DelTo, DelPrm, SiteKey;
        private static int nFiles = 0;

        static void Main(string[] args)
        {
            ReadCfg cfg = new ReadCfg();
            string elsrc = cfg.Get("LogID", "");  // Must be set to write to Windows application log
            string lf = cfg.Get("LogFile");                 // Must be set to write to text log file
            bool wrtcon = cfg.Get("ConLog").ToLower() == "true" ? true : false; // Set to write to console window

            LogWriter log = new LogWriter(lf, wrtcon, elsrc);
            SiteKey = cfg.Get("SiteKey", "<missing>");
            DropFolder = cfg.Get("DropFolder", "<missing>");
            FNPattern = cfg.Get("FNPattern", "*.csv");
            ColNum = cfg.Get("Column", 4) - 1;
            ArchiveFolder = cfg.Get("ArchiveFolder", "<missing>");
            DeliverTo = cfg.Get("DeliverTo", "api");
            MaxErrs = cfg.Get("MaxErrs", 10);
            if (DeliverTo != null && DeliverTo.StartsWith("file://"))
            {
                DelTo = "file";
                DelPrm = DeliverTo.Remove(0, 7);
            }
            else if (DeliverTo == "api")
            {
                DelTo = DeliverTo;
            }
            else
            {
                log.WinLog("ERROR: Parameter DeliverTo is invalid.", "E", true);
            }       

            if (DropFolder == "<missing>" || ArchiveFolder == "<missing>" || SiteKey == "<missing>")
            {
                log.WinLog("ERROR: Required parameter is missing.", "E", true);
            }
            // logic start
            string[] files = Directory.GetFiles(DropFolder, FNPattern);
            var UpdatePaymentPlan = new UpdateFinancialPymtPlan(SiteKey, MaxErrs, log);
            foreach (string filename in files)
            {
                nFiles++;
                FileInfo src = new FileInfo(filename);
                FileInfo Arch = new FileInfo(Path.Combine(ArchiveFolder, src.Name));
                try
                {
                    UpdatePaymentPlan.PaymentPlan(filename, ColNum, DelTo, DelPrm);
                    log.Write("Moving file to archive: {0}", src.FullName);
                    try
                    {
                        if (Arch.Exists)
                        {
                            log.Write("Removing prior archive: {0}", Arch.FullName);
                            Arch.Delete();
                        }
                        src.MoveTo(Arch.FullName);
                    }
                    catch (Exception ex)
                    {
                        log.WinLog(ex, "E", true);
                    }
                }
                catch (Exception ex)
                {
                    log.WinLog(ex, "E", false);
                }
            }
            log.Write("Input files processed - {0}", nFiles);
            log.Write("Errors - {0}", UpdatePaymentPlan.GetErrs());
            if (wrtcon)
            {
                //System.Threading.Thread.Sleep(5000); // pause a little while
                Console.Write("Press any key to exit.");
                Console.ReadKey();
            }
        }
    }

    public class UpdateFinancialPymtPlan
    {
        //private string _apiMessage1 = "<Message MessageType=\"LinkDocument\" NodeID=\"1\" ReferenceNumber=\"MODoc\" UserID=\"1\" Source=\"Tyler\"><DocumentID>%DocumentID%</DocumentID><Entities><Entity><EntityType>Event</EntityType><EntityID>%EntityID%</EntityID></Entity></Entities></Message>";

        private string _apiMessage = "<Message MessageType=\"UpdateFinancialPaymentPlan\" NodeID=\"%NodeID%\" UserID=\"1\" ReferenceNumber=\"0\" Source=\"Tyler\"><PaymentPlanID>%PaymentPlanID%</PaymentPlanID><Schedules><Edit><Schedule><ScheduleID>%PaymentScheduleID%</ScheduleID><RecalculationInfo><Automatic><CalculateSchedule><PaymentAmount>%PaymentAmount%</PaymentAmount></CalculateSchedule><Fees><Add><Fee><FeeInstanceID>%FeeInstanceID%</FeeInstanceID></Fee></Add></Fees><InitialPayment><InitialPaymentType>NONE</InitialPaymentType><InitialPaymentDetail><InitialPaymentAmount>0.00</InitialPaymentAmount></InitialPaymentDetail></InitialPayment><FinalPayment><FinalPaymentType>NONE</FinalPaymentType><FinalPaymentDetail><FinalPaymentAmount>0.00</FinalPaymentAmount></FinalPaymentDetail></FinalPayment></Automatic></RecalculationInfo><Comment>Added CA Balance as of %CurrentDate% per AllianceOne records</Comment></Schedule></Edit></Schedules></Message>";
        private string apiMessage;
        private string _siteKey;
        private string resp;
        private StreamWriter ResFile;
        private LogWriter log;
        private bool WrtLog;
        private int _Errs, _mxErrs;
        private
            OdysseyAPIServiceReference.APIWebServiceSoapClient client;

            
        public UpdateFinancialPymtPlan  (string SiteKey, int maxErrs, LogWriter l = null)
        {
            _siteKey = SiteKey;
            _mxErrs = maxErrs;
            log = l;
            WrtLog = l == null ? false : true;
            _Errs = 0;
        }

        public int GetErrs() { return _Errs; }
        public void PaymentPlan(string file, int Col, string DelTo, string DelPrm)
        {
            string[] rowData;

            if (DelTo == "file")
            {
                log.Write("Opening: {0}", DelPrm);
                ResFile = new StreamWriter(DelPrm);
            }
            else if (DelTo == "api")
            {
                client = new OdysseyAPIServiceReference.APIWebServiceSoapClient();
            }
            //Parse file to get DocumentID
            var results = new List<Payment>();
            try
            {
                Payment Finacialpayment;
                using (var parser = new TextFieldParser(new FileStream(file, FileMode.Open)))
                {
                    parser.Delimiters = new string[] { "," };
                    parser.HasFieldsEnclosedInQuotes = true;
                    parser.TrimWhiteSpace = true;
                    log.Write("Reading: {0}", file);

                    // Call ReadLine() to skip the header row.
                    parser.ReadLine();

                    while (!parser.EndOfData)
                    {
                        Finacialpayment = new Payment();

                        rowData = parser.ReadFields();
                        Finacialpayment.PaymentPlanID = rowData[0];
                        Finacialpayment.NodeID = rowData[6];
                        Finacialpayment.PaymentScheduleID = rowData[1];
                        Finacialpayment.PaymentAmt = rowData[8];
                        Finacialpayment.FeeInstanceID = rowData[3];
                        Finacialpayment.CaseNbr = rowData[4];
                        results.Add(Finacialpayment);
                    }
                }
                if (results.Count != 0)
                {
                    foreach (var payment in results)
                    {
                        if (_Errs >= _mxErrs && _mxErrs > 0)
                        {
                            throw new ArgumentException(string.Format("Error limit of {0} has been reached.", _Errs));
                            //log.Write("Error limit of {0} have been reached.", _Errs);
                            //return;
                        }
                        try
                        {

                            log.Write("Processing Payment Plan id: {0}", payment.PaymentPlanID);
                            apiMessage = _apiMessage.Replace("%PaymentPlanID%", payment.PaymentPlanID);
                            apiMessage = apiMessage.Replace("%PaymentScheduleID%", payment.PaymentScheduleID);
                            apiMessage = apiMessage.Replace("%PaymentAmount%", payment.PaymentAmt);
                            apiMessage = apiMessage.Replace("%FeeInstanceID%", payment.FeeInstanceID);
                            apiMessage = apiMessage.Replace("%CurrentDate%", DateTime.Now.ToString("MM/dd/yyyy"));
                            resp = apiMessage.Replace("%NodeID%", payment.NodeID);


                            if (DelTo == "file")
                            {
                                ResFile.Write(resp + "\n\r");
                            }
                            else
                            {
                                try
                                {
                                    client.OdysseyMsgExecutionAllowFaults(resp, _siteKey);
                                }
                                catch (FaultException ex)
                                {
                                    _Errs++;
                                    //commented as we need to run the application as admin to write to event logs
                                    //log.WinLog(ex, "E", true);
                                    //EventLog el = new EventLog("Application");
                                    //el.Source = "UpdateFinacialPaymentPlan";
                                    //el.WriteEntry(ex.Message + "\n" + ex.StackTrace, EventLogEntryType.Error);
                                    log.Write("Error in Oyssey API call for CaseNumber:{0} \n\r Message:{1}",payment.CaseNbr,ex.Message);
                                }
                            }
                        }   

                        catch (Exception ex)
                        {
                            _Errs++;
                            //commented as we need to run the application as admin to write to event logs
                            //log.WinLog(ex, "E");
                            log.Write("Error: {0} ", ex.Message);
                        }
                    } //foreach
                }
                else
                {
                    throw new ArgumentException(string.Format("File doesn't contain any records"));
                }

            }
            catch (Exception ex)
            {
                log.WinLog(ex, "E");
            }
            if (DelTo == "file")
            {
                ResFile.Close();
            }
        }

        internal class Payment
        {
            public string PaymentPlanID { get; set; }
            public string NodeID { get; set; }
            public string PaymentScheduleID { get; set; }
            public string PaymentAmt { get; set; }
            //public int CaseID{ get; set; }
            public string FeeInstanceID { get; set; }
            public string CaseNbr{ get; set; }
            //public int FeeID{ get; set; }

        }
    }
}
