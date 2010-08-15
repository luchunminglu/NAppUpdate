using System;
using System.Diagnostics;
using System.IO;

using System.Net;
using System.Net.Sockets;

// Used for the named pipes implementation
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;

namespace NAppUpdate.Framework
{
    /// <summary>
    /// Downloads and starts the update process
    /// </summary>
    public class UpdateStarter
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern SafeFileHandle CreateNamedPipe(
           String pipeName,
           uint dwOpenMode,
           uint dwPipeMode,
           uint nMaxInstances,
           uint nOutBufferSize,
           uint nInBufferSize,
           uint nDefaultTimeOut,
           IntPtr lpSecurityAttributes);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern int ConnectNamedPipe(
           SafeFileHandle hNamedPipe,
           IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern SafeFileHandle CreateFile(
           String pipeName,
           uint dwDesiredAccess,
           uint dwShareMode,
           IntPtr lpSecurityAttributes,
           uint dwCreationDisposition,
           uint dwFlagsAndAttributes,
           IntPtr hTemplate);

        //private const uint DUPLEX = (0x00000003);
        private const uint WRITE_ONLY = (0x00000002);
        private const uint FILE_FLAG_OVERLAPPED = (0x40000000);

        internal string PIPE_NAME { get { return string.Format("\\\\.\\pipe\\{0}", _syncProcessName); } }

        internal uint BUFFER_SIZE = 4096;

        private readonly string _updaterPath;
        private readonly Dictionary<string, object> _updateData;
        private readonly string _syncProcessName;

        public UpdateStarter(string pathWhereUpdateExeShouldBeCreated,
            Dictionary<string, object> updateData, string syncProcessName)
        {
            _updaterPath = pathWhereUpdateExeShouldBeCreated;
            _updateData = updateData;
            _syncProcessName = syncProcessName;
        }

        public void Start()
        {
            ExtractUpdaterFromResource(); //take the update executable and extract it to the path where it should be created

            using (SafeFileHandle clientPipeHandle = CreateNamedPipe(
                   PIPE_NAME,
                   WRITE_ONLY | FILE_FLAG_OVERLAPPED,
                   0,
                   1, // 1 max instance (only the updater utility is expected to connect)
                   BUFFER_SIZE,
                   BUFFER_SIZE,
                   0,
                   IntPtr.Zero))
            {
                //failed to create named pipe
                if (clientPipeHandle.IsInvalid)
                    return;

                Process.Start(_updaterPath, string.Format(@"""{0}""", _syncProcessName));

                while (true)
                {
                    int success = 0;
                    try
                    {
                        success = ConnectNamedPipe(
                           clientPipeHandle,
                           IntPtr.Zero);
                    }
                    catch { }

                    //failed to connect client pipe
                    if (success != 1)
                        break;

                    //client connection successfull
                    using (FileStream fStream = new FileStream(clientPipeHandle, FileAccess.Write, (int)BUFFER_SIZE, true))
                    {
                        new BinaryFormatter().Serialize(fStream, _updateData);
                        fStream.Close();
                    }
                }
            }
        }

        private void ExtractUpdaterFromResource()
        {
            //store the updater temporarily in the designated folder
            using (var writer = new BinaryWriter(File.Open(_updaterPath, FileMode.Create)))
                writer.Write(NAppUpdate.Framework.Resources.updater);
        }
    }
}