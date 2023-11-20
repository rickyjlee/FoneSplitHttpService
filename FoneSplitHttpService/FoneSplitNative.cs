using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FoneSplitHttpService
{
    public class FoneSplitNative
    {
        public delegate void MultiChanDataCBEventHandler(IntPtr value, int framesize);

        public delegate void RegisterCBEventHandler(IntPtr value, int nLenOutWave, int nNumChannels, int bProcComplete);

        public static RegisterCBEventHandler handler;

        [DllImport("FoneSplitDllWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr CreateFoneMultiChanProc(int SSLSmotthBlock, int BSSFramesSize, int trainlter, int AGC);

        [DllImport("FoneSplitDllWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int DestroyFoneMultiChanProc(IntPtr foneProc);

        [DllImport("FoneSplitDllWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetOutputMultiChanData(IntPtr foneProc, int frameSize, MultiChanDataCBEventHandler cb);

        [DllImport("FoneSplitDllWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Start(IntPtr foneProc, int numChannel);

        [DllImport("FoneSplitDllWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Complete(IntPtr foneProc);

        [DllImport("FoneSplitDllWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int DoSleep(IntPtr foneProc, int time);

        [DllImport("FoneSplitDllWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Process(IntPtr foneProc, IntPtr raw, int len, int end);

        [DllImport("FoneSplitDllWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int RegisterCallBack(IntPtr foneProc, RegisterCBEventHandler cb);

        public static IntPtr NativeCreateFoneMultiChanProc(int SSLSmotthBlock, int BSSFramesSize, int trainlter, int AGC)
        {
            return CreateFoneMultiChanProc(SSLSmotthBlock, BSSFramesSize, trainlter, AGC);
        }
        public static int NativeDestroyFoneMultiChanProc(IntPtr foneProc)
        {
            return DestroyFoneMultiChanProc(foneProc);
        }

        public static int NativeGetOutputMultiChanData(IntPtr foneProc, int frameSize, MultiChanDataCBEventHandler cb)
        {
            return GetOutputMultiChanData(foneProc, frameSize, cb);
        }

        public static int NativeRegisterCallBack(IntPtr foneProc, RegisterCBEventHandler cb)
        {
            handler = new RegisterCBEventHandler(cb);
            return RegisterCallBack(foneProc, cb);
        }

        public static int NativeStart(IntPtr foneProc, int numChannel)
        {
            return Start(foneProc, numChannel);
        }

        public static int NativeDoSleep(IntPtr foneProc, int time)
        {
            return DoSleep(foneProc, time);
        }

        public static int NativeComplete(IntPtr foneProc)
        {
            handler = null;
            return Complete(foneProc);
        }

        public static int NativeProcess(IntPtr foneProc, IntPtr raw, int len, int end)
        {
            return Process(foneProc, raw, len, end);
        }
    }
}
