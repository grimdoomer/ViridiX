﻿using System;
using System.Diagnostics;
using System.IO;
using ViridiX.Linguist.Network;
using ViridiX.Mason.Extensions;
using ViridiX.Mason.Logging;

namespace ViridiX.Linguist.Memory
{
    /// <summary>
    /// Provides streaming access to Xbox memory.
    /// </summary>
    public class XboxMemoryStream : Stream
    {
        #region Fields

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly Xbox _xbox;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly ILogger _logger;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private const long XbeHeaderAddress = 0x10000;

        #endregion

        #region Properties

        /// <summary>
        /// Prevents crashing due to invalid memory address access. Does not protect against certain physical addresses mapped to hardware.
        /// </summary>
        public bool ProtectedMode { get; set; }

        /// <summary>
        /// TODO: description
        /// </summary>
        public sealed override long Position { get; set; }

        /// <summary>
        /// TODO: description
        /// </summary>
        public override bool CanRead => true;

        /// <summary>
        /// TODO: description
        /// </summary>
        public override bool CanSeek => true;

        /// <summary>
        /// TODO: description
        /// </summary>
        public override bool CanWrite => true;

        /// <summary>
        /// TODO: description
        /// </summary>
        public override bool CanTimeout => true;

        /// <summary>
        /// TODO: description
        /// </summary>
        public override int ReadTimeout
        {
            get
            {
                return _xbox?.CommandSession.ReceiveTimeout ?? 0;
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// TODO: description
        /// </summary>
        public override int WriteTimeout
        {
            get
            {
                return _xbox?.CommandSession.SendTimeout ?? 0;
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// TODO: description
        /// </summary>
        /// <param name="xbox"></param>
        public XboxMemoryStream(Xbox xbox)
        {
            if (xbox == null)
                throw new ArgumentNullException(nameof(xbox));

            _xbox = xbox;
            _logger = xbox.Logger;

            Position = XbeHeaderAddress;    // start at a valid memory address
            ProtectedMode = _xbox.CommandSession.Options.HasFlag(XboxConnectionOptions.ProtectedMode);
        }

        #endregion

        #region Methods

        /// <summary>
        /// TODO: description
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="origin"></param>
        /// <returns></returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin: return Position = (uint)offset; // zero-based address
                case SeekOrigin.Current: return Position += (uint)offset;
                default: throw new InvalidOperationException("Invalid SeekOrigin.");
            }
        }

        /// <summary>
        /// TODO: description
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            // Read data and update stream position.
            int bytesRead = ReadBytes(this.Position, buffer, offset, count);
            this.Position += bytesRead;

            // Return the number of bytes read.
            return bytesRead;
        }

        /// <summary>
        /// TODO: description
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            // Write data and update stream position.
            this.Position += WriteBytes(this.Position, buffer, offset, count);
        }

        /// <summary>
        /// Reads memory from the xbox console
        /// </summary>
        /// <param name="address">Address to start reading from</param>
        /// <param name="buffer">Buffer to hold data read</param>
        /// <param name="bufferOffset">Offset into <see cref="buffer"/> to start writing at</param>
        /// <param name="count">Number of bytes to read</param>
        /// <returns>Number of bytes read</returns>
        private int ReadBytes(long address, byte[] buffer, int bufferOffset, int count)
        {
            if (ProtectedMode && !_xbox.Memory.IsValidAddressRange(address, address + count))
            {
                throw new Exception("Invalid address detected during memory read.");
            }

            // TODO: protected mode getmem using patched xbdm.dll

            // Read the data in blocks to support large reads.
            int bytesRead = 0;
            do
            {
                // Read a full block or the remaining size.
                int blockSize = Math.Min(1024, count - bytesRead);
                _xbox.CommandSession.SendCommandStrict("getmem2 addr={0} length={1}", (address + bytesRead).ToHexString(), blockSize);
                bytesRead += _xbox.CommandSession.Stream.Read(buffer, bufferOffset + bytesRead, blockSize);
            }
            while (bytesRead < count);

            // Return the number of bytes read.
            return bytesRead;
        }

        /// <summary>
        /// Writes an array of bytes to xbox memory
        /// </summary>
        /// <param name="address">Address to write data to</param>
        /// <param name="buffer">Data to be written</param>
        /// <param name="bufferOffset">Offset into <see cref="buffer"/> to start reading from</param>
        /// <param name="count">Number of bytes to write</param>
        /// <returns>Number of bytes written</returns>
        private int WriteBytes(long address, byte[] buffer, int bufferOffset, int count)
        {
            if (ProtectedMode && !_xbox.Memory.IsValidAddressRange(address, address + count))
            {
                throw new Exception("Invalid address detected during memory write.");
            }

            // TODO: faster binary setmem with protected mode support using patched xbdm.dll
            const int maxBytesPerLine = 240;
            int totalWritten = 0;
            while (totalWritten < count)
            {
                int bytesToWrite = Math.Min(maxBytesPerLine, count - totalWritten);
                string hexData = buffer.ToHexString(bufferOffset + totalWritten, bytesToWrite);
                _xbox.CommandSession.SendCommandStrict("setmem addr={0} data={1}", (address + totalWritten).ToHexString(), hexData);
                totalWritten += bytesToWrite;
            }

            // Return the number of bytes written.
            return totalWritten;
        }

        #endregion

        #region Unsupported

        /// <summary>
        /// TODO: description. possibly remove exception and just do nothing
        /// </summary>
        public override void Flush() { throw new NotSupportedException(); }

        /// <summary>
        /// TODO: description. possibly return total memory size
        /// </summary>
        public override long Length { get { throw new NotSupportedException(); } }

        /// <summary>
        /// TODO: description
        /// </summary>
        /// <param name="value"></param>
        public override void SetLength(long value) { throw new NotSupportedException(); }

        #endregion
    }
}
