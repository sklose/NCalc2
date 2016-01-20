using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Antlr3.Runtime.PCL.Output
{
    internal class OutOutputStream : IOutputStream
    {
        public void WriteLine()
        {
            this.WriteLine(string.Empty);
        }

        public void WriteLine(string text)
        {
            OutputStreamHost.WriteLine(text);
        }

        public void WriteLine(object someObject)
        {
            if (someObject != null)
            {
                OutputStreamHost.WriteLine(someObject.ToString());
            }
            OutputStreamHost.WriteLine();
        }

        public void Write(string text)
        {
            OutputStreamHost.Write(text);
        }

        public void ReportProgress(double progress, string key, string message)
        {
            OutputStreamHost.ReportProgress(progress, key, message);
        }
    }
}
