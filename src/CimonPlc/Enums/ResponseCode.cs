namespace CimonPlc.Enums
{
    /// <summary>
    ///     Error Code From Cimon PLC on read &amp; write
    /// </summary>
    public enum ResponseCode
    {
        /// <summary>
        ///     No Error, the response in case that the command is successfully processed.
        /// </summary>
        Success = 0,
        /// <summary>
        ///     Error in system (No link with CPU).
        /// </summary>
        SystemError = 1,
        /// <summary>
        ///     Invalid Device Prefix
        /// </summary>
        InvalidDevicePrefix = 2,
        /// <summary>
        ///     Invalid Device Address
        /// </summary>
        InvalidDeviceAddress = 3,
        /// <summary>
        ///     Error in requested data size
        /// </summary>
        UdpErrorReadDataSize = 4,
        /// <summary>
        ///     Over 16 requested blocks
        /// </summary>
        UdpErrorBlockSize = 5,
        /// <summary>
        ///     The case that buffer memory send an error in data and size
        /// </summary>
        BufferError = 6,
        /// <summary>
        ///     Over receiving buffer capacity
        /// </summary>
        OverBufferCapacity = 7,
        /// <summary>
        ///     Over sending time
        /// </summary>
        OverSendingTime = 8,
        /// <summary>
        ///     UDP Error invalid header
        /// </summary>
        UdpInvalidHeader = 9,
        /// <summary>
        ///     Error in Check-Sum (Check-Sum of received data)
        /// </summary>
        ChecksumError = 10,
        /// <summary>
        ///     Error in the information on Frame Length (Total received frame size)
        /// </summary>
        FrameSizeError = 11,
        /// <summary>
        ///     UDP Error in the size to write data
        /// </summary>
        UdpDataSizeError = 12,
        /// <summary>
        ///     Unknown Bit Value (Error in Bit Write Data)
        /// </summary>
        UnknownBitValue = 13,
        /// <summary>
        ///     Unknown Command
        /// </summary>
        UnknownCommand = 14,
        /// <summary>
        ///     Disabling state from writing
        /// </summary>
        WritingError = 15,
        /// <summary>
        ///     Error in CPU process
        /// </summary>
        CpuError = 16
    }
}
