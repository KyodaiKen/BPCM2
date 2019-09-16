using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BPCM
{
    class CSV
    {
        private List<string> m_columns { get; set; }
        private StreamWriter m_textw;
        private string separator;

        public CSV(List<string> columns, StreamWriter stream, string separator = ";")
        {
            this.m_textw = stream;
            this.m_columns = columns;
            this.separator = separator;

            //Check stream
            if (this.m_textw.Equals(null)) throw new IOException("Stream not opened.");

            string header = "";

            //Write column headers
            foreach (string s in this.m_columns)
            {
                if (this.m_columns[this.m_columns.Count - 1].Equals(s) == false)
                    header = header + s + separator;
                else
                    header = header + s;
            }

            m_textw.WriteLine(header);
        }

        public void AddRow(List<string> values)
        {
            if (this.m_textw.Equals(null)) throw new IOException("Stream not opened.");
            if (this.m_columns.Count != values.Count) throw new Exception("There must be the same number of values and columns!");

            string valuelist = "";

            //Write column headers
            foreach (string v in values)
            {
                if (values[values.Count - 1].Equals(v) == false)
                    valuelist = valuelist + v + separator;
                else
                    valuelist = valuelist + v;
            }

            m_textw.WriteLine(valuelist);
        }
    }
}
