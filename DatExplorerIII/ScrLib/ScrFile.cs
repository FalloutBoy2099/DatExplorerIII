namespace ScrLib
{
    using System;
    using System.Collections.Generic;

    /// <summary>A decoded Arcanum script. Collections returned by the reader are read-only.</summary>
    public sealed class ScrFile
    {
        internal ScrFile(UInt32 headerFlags, UInt32 counters, String description, Byte[] descriptionBytes,
            ScriptFlags flags, Int32 capacity, UInt32 serializedPointer, ScrLine[] lines)
        {
            HeaderFlags = headerFlags;
            Counters = counters;
            Description = description;
            DescriptionBytes = Array.AsReadOnly(descriptionBytes);
            Flags = flags;
            Capacity = capacity;
            SerializedPointer = serializedPointer;
            Lines = Array.AsReadOnly(lines);
        }

        public UInt32 HeaderFlags { get; }
        /// <summary>Four packed 8-bit counters, distinct from the file-level flags.</summary>
        public UInt32 Counters { get; }
        public String Description { get; }
        /// <summary>All 40 original bytes, including the terminator and any padding.</summary>
        public IReadOnlyList<Byte> DescriptionBytes { get; }
        public ScriptFlags Flags { get; }
        public Int32 LineCount => Lines.Count;
        /// <summary>Original editor allocation capacity; not the number of serialized lines.</summary>
        public Int32 Capacity { get; }
        /// <summary>Obsolete 32-bit pointer stored in the file. Never dereferenced.</summary>
        public UInt32 SerializedPointer { get; }
        public IReadOnlyList<ScrLine> Lines { get; }

        public Byte GetCounter(Int32 index)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, 4);
            return (Byte)(Counters >> (index * 8));
        }
    }

    /// <summary>A line contains a condition and both branches. Index is zero-based.</summary>
    public sealed record ScrLine(Int32 Index, ScrCondition Condition, ScrAction Then, ScrAction Else);

    /// <summary>
    /// Raw operand. Interpret TypeTag using ScriptValueType or ScriptFocusObject only
    /// when the particular instruction identifies that operand's domain.
    /// </summary>
    public readonly record struct ScrOperand(Byte TypeTag, Int32 Value)
    {
        public override String ToString() => $"tag={TypeTag}: {Value}";
    }

    public abstract class ScrInstruction
    {
        private protected ScrInstruction(Int32 rawOpcode, ScrOperand[] operands)
        {
            RawOpcode = rawOpcode;
            Operands = Array.AsReadOnly(operands);
        }

        public Int32 RawOpcode { get; }
        public IReadOnlyList<ScrOperand> Operands { get; }
        public abstract String NativeName { get; }
        public abstract Boolean IsKnown { get; }
        public override String ToString() => $"{NativeName}({String.Join(", ", Operands)})";
    }

    public sealed class ScrCondition : ScrInstruction
    {
        internal ScrCondition(Int32 opcode, ScrOperand[] operands) : base(opcode, operands) { }
        public ScriptConditionType Type => (ScriptConditionType)RawOpcode;
        public override String NativeName => Type.GetNativeName();
        public override Boolean IsKnown => Enum.IsDefined(Type);
    }

    public sealed class ScrAction : ScrInstruction
    {
        internal ScrAction(Int32 opcode, ScrOperand[] operands) : base(opcode, operands) { }
        public ScriptActionType Type => (ScriptActionType)RawOpcode;
        public override String NativeName => Type.GetNativeName();
        public override Boolean IsKnown => Enum.IsDefined(Type);
    }
}