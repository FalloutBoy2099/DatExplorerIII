namespace DatExplorerIII.Scripts
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// The table of the procedures: their names, flags and where their code starts (and so how long it is).
	/// </summary>
	internal sealed class ProcedureTable
	{
		/// <summary>The most arguments a procedure can have (a bound that a damaged file goes beyond: the real ones have a few).</summary>
		private const UInt32 MaxArguments = 1024;

		private readonly List<ProcedureDescriptor> _procedures = [];
		private readonly List<UInt32> _sizes = [];

		public UInt32 Count => (UInt32)_procedures.Count;
		public UInt32 OffsetOfProcedureSection { get; private set; }
		public ProcedureDescriptor this[UInt32 index] => _procedures[(Int32)index];

		public void Read(BinaryScriptReader reader)
		{
			_procedures.Clear();
			_sizes.Clear();
			UInt32 count = reader.ReadUInt32();
			for (UInt32 index = 0; index < count; index++)
			{
				ProcedureDescriptor procedure = ProcedureDescriptor.Read(reader);
				if (procedure.ArgumentCount > MaxArguments)
					throw new ScriptFormatException("Error: Invalid number of arguments of a procedure");

				_procedures.Add(procedure);
				_sizes.Add(0);
			}

			List<ProcedureBodyOffset> offsets = [];
			for (UInt32 index = 0; index < count; index++)
			{
				ProcedureDescriptor procedure = _procedures[(Int32)index];
				if ((procedure.Type & ObjectAttributes.Import) == 0)
				{
					if (procedure.BodyOffset != 0 && procedure.BodyOffset != reader.Length)
					{
						offsets.Add(new ProcedureBodyOffset(index, procedure.BodyOffset));
					}
					else
					{
						procedure.Type |= ObjectAttributes.NotImplemented;
					}
				}
			}

			offsets.Sort((left, right) => left.BodyOffset == right.BodyOffset ? left.ProcedureIndex.CompareTo(right.ProcedureIndex) : left.BodyOffset.CompareTo(right.BodyOffset));
			offsets.Add(new ProcedureBodyOffset((UInt32)offsets.Count + 1, reader.Length));
			for (Int32 index = 0; index < offsets.Count - 1; index++)
			{
				if (offsets[index + 1].BodyOffset < offsets[index].BodyOffset)
					throw new ScriptFormatException("Error: Procedures are not sorted in ascending address order");

				_sizes[(Int32)offsets[index].ProcedureIndex] = offsets[index + 1].BodyOffset - offsets[index].BodyOffset;
			}

			OffsetOfProcedureSection = 0;
			for (Int32 index = 0; index < _procedures.Count; index++)
			{
				if (_procedures[index].BodyOffset != 0)
				{
					OffsetOfProcedureSection = _procedures[index].BodyOffset;
					break;
				}
			}
		}

		public UInt32 GetProcedureSize(UInt32 index)
		{
			return _sizes[(Int32)index];
		}
	}

	internal readonly struct ProcedureBodyOffset
	{
		public ProcedureBodyOffset(UInt32 procedureIndex, UInt32 bodyOffset)
		{
			ProcedureIndex = procedureIndex;
			BodyOffset = bodyOffset;
		}

		public UInt32 ProcedureIndex { get; }
		public UInt32 BodyOffset { get; }
	}
}