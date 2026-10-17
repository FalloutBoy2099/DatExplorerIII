namespace ScrLib
{
    using System;
    using System.Buffers.Binary;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Reads extracted little-endian Arcanum SCR files, without executing them.</summary>
    public static class ScrReader
    {
        public const Int32 HeaderSize = 64;
        public const Int32 LineSize = 132;
        public const Int32 InstructionSize = 44;
        public const Int32 OperandCount = 8;

        public static ScrFile Read(String path, ScrReaderOptions? options = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            using var stream = File.OpenRead(path);
            return Read(stream, options);
        }

        /// <summary>Starts at the current position and leaves the caller's stream open. Seeking is unnecessary.</summary>
        public static ScrFile Read(Stream stream, ScrReaderOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(stream);
            options = ValidateOptions(options);
            Span<Byte> header = stackalloc Byte[HeaderSize];
            ReadRequired(stream, header, 0);
            Int32 count = ValidateHeader(header, options);
            Byte[] body = new Byte[checked(count * LineSize)];
            ReadRequired(stream, body, HeaderSize);
            if (options.RequireEndOfStream && stream.ReadByte() != -1)
                throw new ScrFormatException("Unexpected data after the final line", HeaderSize + (Int64)body.Length);
            return Decode(header, body, count, options);
        }

        public static ScrFile Read(ReadOnlySpan<Byte> data, ScrReaderOptions? options = null)
        {
            options = ValidateOptions(options);
            if (data.Length < HeaderSize)
                throw new ScrFormatException("Truncated script header", data.Length);
            Int32 count = ValidateHeader(data[..HeaderSize], options);
            Int64 length = HeaderSize + (Int64)count * LineSize;
            if (data.Length < length)
                throw new ScrFormatException("Truncated script lines", data.Length);
            if (options.RequireEndOfStream && data.Length != length)
                throw new ScrFormatException("Unexpected data after the final line", length);
            return Decode(data[..HeaderSize], data.Slice(HeaderSize, count * LineSize), count, options);
        }

        public static async Task<ScrFile> ReadAsync(String path, ScrReaderOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            return await ReadAsync(stream, options, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Reads asynchronously and leaves the caller's stream open.</summary>
        public static async Task<ScrFile> ReadAsync(Stream stream, ScrReaderOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stream);
            options = ValidateOptions(options);
            Byte[] header = new Byte[HeaderSize];
            await ReadRequiredAsync(stream, header, 0, cancellationToken).ConfigureAwait(false);
            Int32 count = ValidateHeader(header, options);
            Byte[] body = new Byte[checked(count * LineSize)];
            await ReadRequiredAsync(stream, body, HeaderSize, cancellationToken).ConfigureAwait(false);
            if (options.RequireEndOfStream && await stream.ReadAsync(new Byte[1], cancellationToken).ConfigureAwait(false) != 0)
                throw new ScrFormatException("Unexpected data after the final line", HeaderSize + (Int64)body.Length);
            cancellationToken.ThrowIfCancellationRequested();
            return Decode(header, body, count, options);
        }

        private static ScrReaderOptions ValidateOptions(ScrReaderOptions? options)
        {
            options ??= new ScrReaderOptions();
            ArgumentNullException.ThrowIfNull(options.DescriptionEncoding);
            ArgumentOutOfRangeException.ThrowIfNegative(options.MaxLineCount);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(options.MaxLineCount, (Int32.MaxValue - HeaderSize) / LineSize);
            return options;
        }

        private static Int32 ValidateHeader(ReadOnlySpan<Byte> header, ScrReaderOptions options)
        {
            Int32 count = ReadInt32(header, 52);
            Int32 capacity = ReadInt32(header, 56);
            if (count < 0)
                throw new ScrFormatException("Negative line count", 52);
            if (count > options.MaxLineCount)
                throw new ScrFormatException($"Line count exceeds configured limit {options.MaxLineCount}", 52);
            if (capacity < count)
                throw new ScrFormatException("Editor capacity is smaller than the line count", 56);
            return count;
        }

        private static ScrFile Decode(ReadOnlySpan<Byte> header, ReadOnlySpan<Byte> body, Int32 count, ScrReaderOptions options)
        {
            Byte[] description = header.Slice(8, 40).ToArray();
            Int32 terminator = Array.IndexOf(description, (Byte)0);
            String text = options.DescriptionEncoding.GetString(description, 0, terminator < 0 ? 40 : terminator);
            var lines = new ScrLine[count];
            for (Int32 index = 0; index < count; index++)
            {
                ReadOnlySpan<Byte> line = body.Slice(index * LineSize, LineSize);
                lines[index] = new ScrLine(index,
                    new ScrCondition(ReadInt32(line, 0), ReadOperands(line)),
                    new ScrAction(ReadInt32(line, 44), ReadOperands(line[44..])),
                    new ScrAction(ReadInt32(line, 88), ReadOperands(line[88..])));
            }
            return new ScrFile(ReadUInt32(header, 0), ReadUInt32(header, 4), text, description,
                (ScriptFlags)ReadUInt32(header, 48), ReadInt32(header, 56), ReadUInt32(header, 60), lines);
        }

        private static ScrOperand[] ReadOperands(ReadOnlySpan<Byte> instruction)
        {
            var operands = new ScrOperand[OperandCount];
            for (Int32 index = 0; index < OperandCount; index++)
                operands[index] = new ScrOperand(instruction[4 + index], ReadInt32(instruction, 12 + 4 * index));
            return operands;
        }

        private static Int32 ReadInt32(ReadOnlySpan<Byte> data, Int32 offset) => BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
        private static UInt32 ReadUInt32(ReadOnlySpan<Byte> data, Int32 offset) => BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);

        private static void ReadRequired(Stream stream, Span<Byte> destination, Int64 offset)
        {
            Int32 total = 0;
            while (total < destination.Length)
            {
                Int32 read = stream.Read(destination[total..]);
                if (read == 0)
                    throw new ScrFormatException("Truncated script", offset + total);
                total += read;
            }
        }

        private static async Task ReadRequiredAsync(Stream stream, Memory<Byte> destination, Int64 offset,
            CancellationToken cancellationToken)
        {
            Int32 total = 0;
            while (total < destination.Length)
            {
                Int32 read = await stream.ReadAsync(destination[total..], cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    throw new ScrFormatException("Truncated script", offset + total);
                total += read;
            }
        }
    }
}