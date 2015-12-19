using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using EnvDTE;

namespace TplBuilder
{
    public static class InfoLog
    {
        private static OutputWindow _outputWindow;

        static InfoLog()
        {
            try
            {
                var window = VisualStudioRef.ApplicationObject.Windows.Item(EnvDTE.Constants.vsWindowKindOutput);
                _outputWindow = window.Object as OutputWindow;
            }
            catch (Exception ex)
            {
                ErrorLog.Log(ex);
            }
        }

        public static void Log(string message)
        {
            try
            {
                if (_outputWindow.ActivePane != null)
                {
                    _outputWindow.ActivePane.Activate();
                    _outputWindow.ActivePane.OutputString(Config.MsgCaption + ": " + message + "\n");
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Log(ex);
            }
        }
    }
}
