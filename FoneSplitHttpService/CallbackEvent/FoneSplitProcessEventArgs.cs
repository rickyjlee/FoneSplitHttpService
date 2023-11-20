using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FoneSplitHttpService.CallbackEvent
{
    public class FoneSplitProcessEventArgs : EventArgs
    {
        private List<byte[]> _buffers;

        public FoneSplitProcessEventArgs(List<byte[]> buffers)
        {
            _buffers = buffers;
        }

        public List<byte[]> Buffers => _buffers;
    }
}
