using System;
using System.IO;
//using System.Reflection;
using System.Diagnostics;

public class LogWriter
{
    private string LogFile = null;
    private string ELSource = null;
    private bool ConMsg = false;
    private bool TxtMsg = false;
    private bool WinMsg = false; // write to system log

    public LogWriter(string FileName = null, bool Con = false, string Src = null)
    {
        LogFile = FileName;
        ConMsg = Con;
        ELSource = Src;
        TxtMsg = (LogFile != null) ? true : false;
        WinMsg = (ELSource != null) ? true : false;
        Write(""); // visible break in the file
    }
    public void Write(string logMessage, params object[] args)
    {
        string res = System.String.Format(logMessage, args);
        if (TxtMsg)
        {
            try
            {
                using (StreamWriter w = File.AppendText(LogFile))
                {
                    Log(w, res);
                }
            }
            catch //(Exception ex)
            {
            }
        }
        if (ConMsg)
        {
            Console.WriteLine(res);
        }
    }

    private void Log(TextWriter txtWriter, string logMessage)
    {
        try
        {
            DateTime dt = DateTime.Now;
            txtWriter.Write(dt.ToString("o"));
            txtWriter.WriteLine("  :{0}", logMessage);
        }
        catch //(Exception ex)
        {
        }
    }

    public void WinLog(Exception ex, string eType = "I", bool Die = false)
    {
        Write("Exception : {0}\n\r Message: {1}\n\r StackTrace: {2}", ex.GetType(), ex.Message, ex.StackTrace);
        if (WinMsg)
        {
            EventLogEntryType elt = EventLogEntryType.Information;
            if (eType == "W")
            {
                elt = EventLogEntryType.Warning;
            }
            else if (eType == "E")
            {
                elt = EventLogEntryType.Error;
            }
            EventLog el = new EventLog("Application");
            el.Source = "Update Financial Payment Plan";
            el.WriteEntry(ex.Message + "\n" + ex.StackTrace, elt);
            if (Die)
            {
                throw ex;
            }
        }
    }
    public void WinLog(string msg, string eType = "I", bool Die = false)
    {
        WinLog(new Exception(msg), eType, Die);
    }
    public void xWinLog(string msg, string eType = "I", bool Die = false)
    {
        Write("Exception : {0}", msg);
        if (WinMsg)
        {
            EventLogEntryType elt = EventLogEntryType.Information;
            if (eType == "W")
            {
                elt = EventLogEntryType.Warning;
            }
            else if (eType == "E")
            {
                elt = EventLogEntryType.Error;
            }
            EventLog el = new EventLog("Application");
            el.Source = this.ELSource;
            el.WriteEntry(msg, elt);
            if (Die)
            {
                throw new Exception(msg);
            }
        }
    }
}
