using System;
using System.Configuration;

namespace UpdateFinancialPaymentPlan
{
    class ReadCfg
    {
        private System.Collections.Specialized.NameValueCollection cfg;
        LogWriter log = null;
        public ReadCfg(LogWriter logger = null)
        {
            log = logger;
            try
            {
                this.cfg = ConfigurationManager.AppSettings;
            }
            catch (System.NullReferenceException) // AppSettings not define in App.config
            {
                cfg = null;
            }
        }

        public string Get(string key, string dflt = null)
        {
            string result;
            try
            {
                var appSettings = ConfigurationManager.AppSettings;
                result = appSettings[key] ?? dflt;
                //Console.WriteLine(result);
            }
            catch (ConfigurationErrorsException)
            {
                Console.WriteLine("Error reading app settings: {0}", key);
                if (log != null)
                {
                    log.Write("... error");
                }
                result = dflt;
            }
            return result;
        }
        public int Get(string key, int dflt)
        {
            int result = dflt;
            string zz = "";
            try
            {
                zz = this.Get(key, "-980");
                result = Convert.ToInt32(zz);
            }
            catch (System.FormatException)
            {
                // invalid format is treated as missing from config
                Console.WriteLine("Error reading invalid integer: {0}, {1}", key, zz);
                if (log != null)
                {
                    log.Write("... error #2");
                }
            }
            return result;
        }
    }
}
