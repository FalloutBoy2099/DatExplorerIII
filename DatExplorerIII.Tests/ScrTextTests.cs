namespace DatExplorerIII.Tests
{
	using System;
	using System.Buffers.Binary;
	using System.Text;
	using DatExplorerIII.Models;
	using DatExplorerIII.Services;
	using ScrLib;

	/// <summary>The scripts of Arcanum (.scr), shown as text like the decompiled scripts.</summary>
	public sealed class ScrTextTests
	{
		/// <summary>A script of two lines: IF DAYTIME THEN GOTO(Number:5) ELSE (nothing); IF HASGOLD(Number:100) THEN RETURN_AND_SKIP_DEFAULT ELSE RETURN_AND_RUN_DEFAULT.</summary>
		private static Byte[] Build()
		{
			Byte[] data = new Byte[ScrReader.HeaderSize + (2 * ScrReader.LineSize)];
			Encoding.ASCII.GetBytes("Test script").CopyTo(data, 8);
			BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(52), 2);
			BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(56), 4);

			void Instruction(Int32 line, Int32 slot, Int32 opcode, Byte tag, Int32 value)
			{
				Int32 offset = ScrReader.HeaderSize + (line * ScrReader.LineSize) + (slot * ScrReader.InstructionSize);
				BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset), opcode);
				data[offset + 4] = tag;
				BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 12), value);
			}

			Instruction(0, 0, (Int32)ScriptConditionType.Daytime, 0, 0);
			Instruction(0, 1, (Int32)ScriptActionType.Goto, (Byte)ScriptValueType.Number, 5);
			Instruction(1, 0, (Int32)ScriptConditionType.HasGold, (Byte)ScriptValueType.Number, 100);
			Instruction(1, 1, (Int32)ScriptActionType.ReturnAndSkipDefault, 0, 0);
			Instruction(1, 2, (Int32)ScriptActionType.ReturnAndRunDefault, 0, 0);
			return data;
		}

		[Fact]
		public void AScript_IsWrittenAsIfThenElse_WithTheNamesOfTheEngine()
		{
			String text = ScrText.Render(Build());
			String[] lines = text.Split(Environment.NewLine);

			Assert.Equal("// Test script", lines[0]);
			Assert.Contains("2 lines, capacity 4", lines[1]);
			Assert.Equal("if SCT_DAYTIME()", lines[4]);
			Assert.Equal("then SAT_GOTO(Number:5)", lines[5]);

			// An else that does nothing is not written; the other one is.
			Assert.Equal("// line 1", lines[7]);
			Assert.Equal("if SCT_HAS_GOLD(Number:100)", lines[8]);
			Assert.Equal("then SAT_RETURN_AND_SKIP_DEFAULT()", lines[9]);
			Assert.Equal("else SAT_RETURN_AND_RUN_DEFAULT()", lines[10]);
		}

		[Fact]
		public void AScript_IsATextFileOfTheViewer_ButNotPlainText_AndIsComparedAsText()
		{
			Assert.True(FileViewers.TryGetKind("39990.scr", out FileViewerKind kind));
			Assert.Equal(FileViewerKind.Text, kind);
			Assert.False(FileViewers.IsPlainText("39990.scr"));
			Assert.True(TextContentComparer.AppliesTo("39990.SCR"));

			Assert.Equal(FileTextResult.Lines, FileTextView.TryGetLines("a.scr", Build(), null, out System.Collections.Generic.IReadOnlyList<String> lines));
			Assert.Contains("then SAT_GOTO(Number:5)", lines);
		}

		[Fact]
		public void ATruncatedScript_IsRefused()
		{
			Assert.Throws<ScrFormatException>(() => ScrText.Render(new Byte[10]));
		}
	}
}
