using System;

namespace FreehandROI
{
    public enum RoiResult
    {
        Closed,
        Escaped
    }

    public class RoiResultEventArgs : EventArgs
    {
        public RoiResultEventArgs(RoiResult result)
        {
            this.Result = result;
        }

        public RoiResult Result { get; private set; }
    }
}