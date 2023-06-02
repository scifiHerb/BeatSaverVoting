using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatSaverVoting.Settings
{
    public class Configuration
    {
        public static Configuration Instance { get; set; } = null;
        public virtual bool showGood { get; set; } = true;
        public virtual bool showBad { get; set; } = false;
    }
}
