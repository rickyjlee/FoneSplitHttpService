using FoneSplitHttpService.CallbackEvent;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FoneSplitHttpService
{
    public class FoneSplitService
    {
        private const int MC_PROC_DATA_END = 2;
        private const int SSLSmotthBlock = 7;
        //private const int BSSFramesSize = 1600;
        private const int BSSFramesSize = 256;
        private const int trainlter = 1;
        private const int AGC = 0;
        private IntPtr _foneProc;
        private Thread _foneProcThread;
        private bool _isUsingFoneSplit;
        private const int BitRate = 8;
        private int _bufferProcessUnit;
        private int _channelCnt;
        //private BlockingCollection<float[]> _blockingCollection;
        private BlockingCollection<byte[]> _blockingCollection;
        private System.Timers.Timer _uploadTimer;

        public event EventHandler<FoneSplitProcessEventArgs> FoneSplitProcessEvent;
        public event EventHandler<FoneSplitStopEventArgs> FoneSplitStopEvent;

        private volatile FoneSplitStatus _foneSplitStatus;

        public FoneSplitService()
        {
            _foneSplitStatus = FoneSplitStatus.Stopped;
            _foneProc = IntPtr.Zero;
        }

        public bool InitializeFoneSplit(int channelCnt)
        {
            if (!FoneSplitLicenseCheck())
                return false;

            _isUsingFoneSplit = channelCnt >= 2 && FoneSplitLicenseCheck();
            _bufferProcessUnit = 9600 * channelCnt;
            _foneProcThread?.Abort();
            _foneProcThread = null;
            _foneProc = FoneSplitNative.NativeCreateFoneMultiChanProc(SSLSmotthBlock, 8, trainlter, AGC);
            _channelCnt = channelCnt;

            FoneSplitNative.NativeStart(_foneProc, channelCnt);

            //FoneSplitNative.NativeRegisterCallBack(_foneProc, FoneSplitCallBackFunc);  //화자분리 콜백 버전
            return _isUsingFoneSplit;
        }

        public void FoneSplitCallBackFunc(IntPtr OutWave, int nLenOutWave, int nNumChannels, int bProcComplete)
        {

            if (nLenOutWave > 0)
            {
                var buffer = new byte[nLenOutWave * nNumChannels * 2];
                Marshal.Copy(OutWave, buffer, 0, buffer.Length);

                var buffers = new List<byte[]>();
                for (int i = 0; i != nNumChannels; i++)
                {
                    var changeByte = new byte[nLenOutWave * 2];
                    var offset = i * changeByte.Length;
                    Buffer.BlockCopy(buffer, offset, changeByte, 0, changeByte.Length);
                    buffers.Add(changeByte);
                }

                FoneSplitProcessEvent?.Invoke(this, new FoneSplitProcessEventArgs(buffers));
            }
        }

        public void FinalizeFoneSplit()
        {
            FoneSplitNative.NativeProcess(_foneProc, IntPtr.Zero, 0, 1);
            Task.Run(() =>
            {
                IntPtr foneProc = _foneProc;
                _foneProc = IntPtr.Zero;

                _foneProcThread?.Join();
                _foneProcThread = null;

                FoneSplitNative.NativeComplete(foneProc);
                if (foneProc != IntPtr.Zero)
                    FoneSplitNative.NativeDestroyFoneMultiChanProc(foneProc);

                FoneSplitStopEvent?.Invoke(this, new FoneSplitStopEventArgs(true));
                //_stoppedException = null;
            });
        }

        private void ProcessFoneSplit(byte[] buffer, int endData = 0)
        {
            short[] sdata = new short[(int)(buffer.Length / 2)];
            Buffer.BlockCopy(buffer, 0, sdata, 0, buffer.Length);
            int size = Marshal.SizeOf(sdata[0]) * sdata.Length;
            IntPtr raw = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.Copy(sdata, 0, raw, sdata.Length);
                FoneSplitNative.NativeProcess(_foneProc, raw, sdata.Length, endData);
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Err!!!!!! {exception.Message}");
            }
            finally
            {
                Marshal.FreeHGlobal(raw);
            }
        }
       
        private bool FoneSplitLicenseCheck()
        {
            var directorySeparator = Path.DirectorySeparatorChar.ToString();
            var licenceFolderName = "license";
            var rootDirectory = Directory.GetCurrentDirectory();
            var rootLicenseDirectory = $"{rootDirectory}{directorySeparator}{licenceFolderName}";

            if (Directory.Exists(rootLicenseDirectory))
            {
                return true;
            }
            return false;
        }

        public bool StartRecording()
        {
            if (_foneSplitStatus != FoneSplitStatus.Stopped)
                return true;

            _foneSplitStatus = FoneSplitStatus.Processing;

            BufferPoolingRunTask();
            if (!_isUsingFoneSplit) InitializeFoneSplit(2);
            return true;
        }

        public bool StopRecording()
        {
            if (_foneSplitStatus != FoneSplitStatus.Processing)
                return true;

            _foneSplitStatus = FoneSplitStatus.Stopping;
            _blockingCollection.CompleteAdding();
            return true;
        }

        public void SendBuffer(byte[] buffer)
        {      
            //var samples = new float[ch * bfSize];
            //var count = e.GetAsInterleavedSamples(samples);
            if(_blockingCollection != null)
                _blockingCollection.Add(buffer);
        }

        private void BufferPoolingRunTask()
        {
            _blockingCollection = new BlockingCollection<byte[]>();
            Task.Run(() =>
            {
                var bufferArray = new List<byte>();
                while (_blockingCollection != null && !_blockingCollection.IsCompleted)
                {
                    try
                    {
                        if (_blockingCollection.Count <= 0)
                            continue;
                        var samples = _blockingCollection.Take();
                        //var byteArray = ConvertSamplesToBytes(samples);
                        var byteArray = samples;

                        //bufferArray.AddRange(byteArray);
                        //if (bufferArray.Count < _bufferProcessUnit)
                            //continue;

                        ProcessBuffer(samples);
                        //bufferArray.RemoveRange(0, _bufferProcessUnit);


                        //ProcessBuffer(bufferArray.ToArray());
                        //bufferArray.Clear();
                    }
                    catch (InvalidOperationException)
                    {
                        //break;
                    }
                }

                if (bufferArray.Count > 0)
                {
                    ProcessBuffer(bufferArray.ToArray(), 1);
                    bufferArray.Clear();
                }

                if (_isUsingFoneSplit)
                {
                    _uploadTimer = new System.Timers.Timer(200);
                    _uploadTimer.Elapsed += (sender, args) => {
                        FinalizeFoneSplit();

                        _uploadTimer.Stop();
                        _uploadTimer.Dispose();
                    };
                    _uploadTimer.Start();
                    _blockingCollection?.Dispose();
                    return;
                }

                FoneSplitStopEvent?.Invoke(this, new FoneSplitStopEventArgs(true));
                _blockingCollection?.Dispose();
            });
        }

        private void ProcessBuffer(byte[] buffer, int endData = 0)
        {
            //var resampleBuffers = IS_BACKUP_ORIGINAL_16K_DATA ? Resampling(buffer, _deviceWaveFormat, _recognizeWaveFormat.SampleRate) : buffer;
            var resampleBuffers = buffer;

            if (_isUsingFoneSplit)
            {
                ProcessFoneSplit(resampleBuffers, endData);
            }
            var original_buffers = SplitChannels(resampleBuffers, _channelCnt);
            FoneSplitProcessEvent?.Invoke(this, new FoneSplitProcessEventArgs(original_buffers));

            if (!_isUsingFoneSplit)
            {
                FoneSplitProcessEvent?.Invoke(this, new FoneSplitProcessEventArgs(original_buffers));
            }
            return;
            

            //var buffers = SplitChannels(resampleBuffers, _deviceWaveFormat);
            //RecordDataAvailable?.Invoke(this, new AudioRecordSplitterEventArgs(buffers));
        }

        private byte[] ConvertSamplesToBytes(float[] samples)
        {
            using (var stream = new MemoryStream())
            {
                foreach (var sample in samples)
                {
                    var value = BitConverter.GetBytes((Int16)(Int16.MaxValue * sample));
                    stream.Write(value, 0, value.Length);
                }
                return stream.ToArray();
            }
        }

        /*
        private byte[] Resampling(byte[] byteArray, WaveFormat original, int outRate)
        {
            if (original.Channels == 2)
            {
                var resampleStream = new AcmStream(new WaveFormat(original.SampleRate, 16, original.Channels),
                    new WaveFormat(outRate, 16, original.Channels));
                Buffer.BlockCopy(byteArray, 0, resampleStream.SourceBuffer, 0, byteArray.Length);

                int sourceBytesConverted = 0;
                var convertedBytes = resampleStream.Convert(byteArray.Length, out sourceBytesConverted);
                if (sourceBytesConverted != byteArray.Length)
                {
                    Console.WriteLine("Resampling");
                }
                var converted = new byte[convertedBytes];
                Buffer.BlockCopy(resampleStream.DestBuffer, 0, converted, 0, convertedBytes);

                return converted;
            }
            else
            {
                var split_buffers = SplitChannels(byteArray, _deviceWaveFormat);
                var outBuffers = new List<byte[]>();
                for (int j = 0; j < original.Channels; j++)
                {
                    var resampleStream = new AcmStream(new WaveFormat(original.SampleRate, 16, 1),
                      new WaveFormat(outRate, 16, 1));
                    Buffer.BlockCopy(split_buffers[j], 0, resampleStream.SourceBuffer, 0, split_buffers[j].Length);

                    int sourceBytesConverted = 0;
                    var convertedBytes = resampleStream.Convert(split_buffers[j].Length, out sourceBytesConverted);
                    if (sourceBytesConverted != split_buffers[j].Length)
                    {
                        Console.WriteLine("Resampling");
                    }
                    var converted = new byte[convertedBytes];
                    Buffer.BlockCopy(resampleStream.DestBuffer, 0, converted, 0, convertedBytes);
                    outBuffers.Add(converted);
                }
                var combine_buffers = new List<byte>();
                for (var i = 0; i < outBuffers[0].Length; i += 2)
                {
                    for (var j = 0; j != original.Channels; j++)
                    {
                        combine_buffers.Add(outBuffers[j][i]);
                        combine_buffers.Add(outBuffers[j][i + 1]);
                    }
                }
                return combine_buffers.ToArray();
            }
        }*/

        private List<byte[]> SplitChannels(byte[] byteArray, int channelCnt)
        {
            var outBuffers = new List<byte[]>();
            List<byte>[] bufferArray = new List<byte>[channelCnt];
            for (var i = 0; i < bufferArray.Length; i++)
            {
                bufferArray[i] = new List<byte>();
            }

            using (var inputStream = new MemoryStream(byteArray))
            using (var binReader = new BinaryReader(inputStream))
            {
                var readUnit = 16 / 8;
                binReader.BaseStream.Position = 0;

                var index = 0;
                var offset = 0;
                while (index < byteArray.Length)
                {
                    for (var j = 0; j < channelCnt; j++)
                    {
                        var bytes = binReader.ReadBytes(readUnit);
                        bufferArray[j].AddRange(bytes);
                        index += readUnit;
                    }
                    offset++;
                }

                foreach (var array in bufferArray)
                {
                    outBuffers.Add(array.ToArray());
                }

                return outBuffers;
            }
        }
    }
}
