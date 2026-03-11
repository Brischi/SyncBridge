using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncBridge.ApplicationLayer
{
    public class SyncResult
    {
        public int Deactivated { get; set; }
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Total => Deactivated + Created + Updated;
        public bool IsUpToDate => Total == 0;
    }
}