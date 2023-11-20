using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FoneSplitHttpService.CallbackEvent
{
    public class FoneSplitStopEventArgs : EventArgs
    {
        private bool _isSuccess;
        public FoneSplitStopEventArgs(bool isSuccess)
        {
            _isSuccess = isSuccess;
        }

        public bool IsSuccess => _isSuccess;

    }
}
