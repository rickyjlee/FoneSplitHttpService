using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FoneSplitHttpService
{
    public enum FoneSplitStatus
    {
        /// <summary>Not recording</summary>
        Stopped,
        /// <summary>Beginning to stop</summary>
        Stopping,
        /// <summary>Beginning to record</summary>
        Processing,
    }
}
