using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeamGipsy.Model.SqliteControl;

namespace TeamGipsy.Model.PushControl
{
    class WordType
    {
        public int Number;
        public List<Word> WordList = null;
        public List<CustomizeWord> CustWordList = null;
    }
}
