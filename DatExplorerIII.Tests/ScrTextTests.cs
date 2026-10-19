namespace DatExplorerIII.Tests
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using DatExplorerIII.Models;
	using DatExplorerIII.Services;
	using ScrLib;

	/// <summary>The scripts of Arcanum (.scr), shown as text like the decompiled scripts.</summary>
	public sealed class ScrTextTests
	{
		/// <summary>A script of two lines: IF DAYTIME THEN GOTO(Number:5) ELSE (nothing); IF HASGOLD(the attachee, Number:100) THEN RETURN_AND_SKIP_DEFAULT ELSE RETURN_AND_RUN_DEFAULT.</summary>
		private static Byte[] Build()
		{
			Byte[] data = new Byte[ScrReader.HeaderSize + (2 * ScrReader.LineSize)];
			Encoding.ASCII.GetBytes("Test script").CopyTo(data, 8);
			BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(52), 2);
			BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(56), 4);

			void Instruction(Int32 line, Int32 slot, Int32 opcode, Byte tag, Int32 value, Int32 operand = 0)
			{
				Int32 offset = ScrReader.HeaderSize + (line * ScrReader.LineSize) + (slot * ScrReader.InstructionSize);
				BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset), opcode);
				data[offset + 4 + operand] = tag;
				BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 12 + (4 * operand)), value);
			}

			Instruction(0, 0, (Int32)ScriptConditionType.Daytime, 0, 0);
			Instruction(0, 1, (Int32)ScriptActionType.Goto, (Byte)ScriptValueType.Number, 5);

			// The first operand of "has gold" is the object, the second the amount.
			Instruction(1, 0, (Int32)ScriptConditionType.HasGold, (Byte)ScriptFocusObject.Attachee, 0);
			Instruction(1, 0, (Int32)ScriptConditionType.HasGold, (Byte)ScriptValueType.Number, 100, 1);
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
			Assert.Equal("if SCT_HAS_GOLD(Attachee, Number:100)", lines[8]);
			Assert.Equal("then SAT_RETURN_AND_SKIP_DEFAULT()", lines[9]);
			Assert.Equal("else SAT_RETURN_AND_RUN_DEFAULT()", lines[10]);
		}

		/// <summary>A script of one line: the condition and the "then" action are given operand by operand (a type and a number).</summary>
		private static Byte[] BuildLine(ScriptConditionType condition, (Byte Tag, Int32 Value)[] conditionOperands, ScriptActionType action, (Byte Tag, Int32 Value)[] actionOperands, UInt32 headerFlags = 0, UInt32 counters = 0)
		{
			Byte[] data = new Byte[ScrReader.HeaderSize + ScrReader.LineSize];
			BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0), headerFlags);
			BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), counters);
			BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(52), 1);
			BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(56), 1);

			void Write(Int32 slot, Int32 opcode, (Byte Tag, Int32 Value)[] operands)
			{
				Int32 offset = ScrReader.HeaderSize + (slot * ScrReader.InstructionSize);
				BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset), opcode);
				for (Int32 index = 0; index < operands.Length; index++)
				{
					data[offset + 4 + index] = operands[index].Tag;
					BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 12 + (4 * index)), operands[index].Value);
				}
			}

			Write(0, (Int32)condition, conditionOperands);
			Write(1, (Int32)action, actionOperands);
			return data;
		}

		[Fact]
		public void TheOperandsThatAreObjects_HaveTheNameOfTheObject_WhateverNumberWasLeftInTheFile()
		{
			// "npc is a follower of pc": two objects. The numbers are what was in the memory of the editor when the file was saved.
			Byte[] data = BuildLine(
				ScriptConditionType.Eq,
				[],
				ScriptActionType.DoNothing,
				[]);
			Assert.Contains("if SCT_EQ(Counter:0, Counter:0)", ScrText.Render(data));

			ScriptConditionType following = Enum.GetValues<ScriptConditionType>().Single(type => type.GetNativeName() == "SCT_OBJ_FOLLOWING_PC");
			String text = ScrText.Render(BuildLine(
				following,
				[((Byte)ScriptFocusObject.Attachee, 4695986), ((Byte)ScriptFocusObject.Player, 15613440)],
				ScriptActionType.DoNothing,
				[]));

			Assert.Contains("if SCT_OBJ_FOLLOWING_PC(Attachee, Player)", text);
			Assert.DoesNotContain("4695986", text, StringComparison.Ordinal);
		}

		[Fact]
		public void ALocalObject_SaysWhichOne_AndAnObjectThatTheGameDoesNotKnow_IsNamedByItsType()
		{
			ScriptConditionType dead = Enum.GetValues<ScriptConditionType>().Single(type => type.GetNativeName() == "SCT_OBJ_IS_DEAD");

			Assert.Contains("if SCT_OBJ_IS_DEAD(LocalObject:3)", ScrText.Render(BuildLine(dead, [((Byte)ScriptFocusObject.LocalObject, 3)], ScriptActionType.DoNothing, [])));
			Assert.Contains("if SCT_OBJ_IS_DEAD(Object99:7)", ScrText.Render(BuildLine(dead, [(99, 7)], ScriptActionType.DoNothing, [])));
		}

		[Fact]
		public void AValueThatIsNotKnown_IsNamedByItsType_AndAVariableOfThePlayer_SaysWhichObjectHoldsIt()
		{
			String unknown = ScrText.Render(BuildLine(ScriptConditionType.Eq, [(200, 5), ((Byte)ScriptValueType.Number, 6)], ScriptActionType.DoNothing, []));
			Assert.Contains("if SCT_EQ(Tag200:5, Number:6)", unknown);

			// A variable of the player: the object is in the high half of the number, the variable in the other.
			Int32 variable = ((Int32)ScriptFocusObject.Player << 16) | 17;
			String player = ScrText.Render(BuildLine(ScriptConditionType.Eq, [((Byte)ScriptValueType.PcVar, variable), ((Byte)ScriptValueType.Number, 1)], ScriptActionType.DoNothing, []));
			Assert.Contains("if SCT_EQ(PcVar:Player.17, Number:1)", player);
		}

		[Fact]
		public void TheLocalFlagsAndTheCountersOfTheHeader_AreSaid_WhenThereIsSomething()
		{
			String plain = ScrText.Render(BuildLine(ScriptConditionType.True, [], ScriptActionType.DoNothing, []));
			Assert.DoesNotContain("counters", plain, StringComparison.Ordinal);
			Assert.DoesNotContain("local flags", plain, StringComparison.Ordinal);

			String flags = ScrText.Render(BuildLine(ScriptConditionType.True, [], ScriptActionType.DoNothing, [], headerFlags: 0xE0000000));
			Assert.Contains("// local flags 0xE0000000", flags);
			Assert.DoesNotContain("counters", flags, StringComparison.Ordinal);

			// The four counters are the four bytes of the number: 0x14000000 is a counter 3 of 20.
			String counters = ScrText.Render(BuildLine(ScriptConditionType.True, [], ScriptActionType.DoNothing, [], counters: 0x14000000));
			Assert.Contains("// counters 0, 0, 0, 20", counters);

			String both = ScrText.Render(BuildLine(ScriptConditionType.True, [], ScriptActionType.DoNothing, [], headerFlags: 0x10, counters: 0x0A));
			Assert.Contains("// local flags 0x00000010; counters 10, 0, 0, 0", both);
		}

		[Fact]
		public void EveryInstruction_HasItsOperandsDescribed_AsManyAsTheGameReads()
		{
			List<String> names = Enum.GetValues<ScriptConditionType>().Select(type => type.GetNativeName())
				.Concat(Enum.GetValues<ScriptActionType>().Select(type => type.GetNativeName()))
				.ToList();

			Assert.True(names.Count > 150);
			foreach (String name in names)
			{
				Assert.True(ScrOperandCounts.TryGet(name, out Int32 count), name + " has no count of operands.");
				Assert.True(ScrOperandDomains.TryGet(name, out String domains), name + " has no kinds of operands.");
				Assert.True(domains.Length == count, name + " reads " + count + " operands, and " + domains.Length + " have a kind.");
				Assert.True(domains.All(kind => kind is 'O' or 'V'), name + " has a kind that is not an object or a value.");
			}

			// What the code of the game reads as an object, and as a value.
			ScrOperandDomains.TryGet("SCT_HAS_GOLD", out String gold);
			Assert.Equal("OV", gold);
			ScrOperandDomains.TryGet("SAT_CALL_SCRIPT_AT", out String call);
			Assert.Equal("VVOOV", call);
			ScrOperandDomains.TryGet("SAT_ATTACK", out String attack);
			Assert.Equal("OO", attack);
		}

		private static ScriptActionType Action(String nativeName) => Enum.GetValues<ScriptActionType>().Single(type => type.GetNativeName() == nativeName);

		[Fact]
		public void ACallOfAScript_SaysWhichScriptIsCalled_WhenItsNumberIsOneOfAFile()
		{
			(Byte, Int32)[] call = [((Byte)ScriptValueType.Number, 1013), ((Byte)ScriptValueType.Number, 0), ((Byte)ScriptFocusObject.Triggerer, 0), ((Byte)ScriptFocusObject.Attachee, 0)];
			Byte[] data = BuildLine(ScriptConditionType.True, [], Action("SAT_CALL_SCRIPT"), call);
			Func<Int32, String?> names = number => number == 1013 ? "01013dante.scr" : null;

			Assert.Contains("then SAT_CALL_SCRIPT(Number:1013, Number:0, Triggerer, Attachee)  // 01013dante.scr", ScrText.Render(data, names));

			// Nothing to say without the names, for a number that is not a script, or when the number is not written in the file.
			Assert.DoesNotContain("//", ScrText.Render(data).Split(Environment.NewLine).Single(line => line.StartsWith("then ", StringComparison.Ordinal)), StringComparison.Ordinal);
			Assert.DoesNotContain("dante", ScrText.Render(BuildLine(ScriptConditionType.True, [], Action("SAT_CALL_SCRIPT"), [((Byte)ScriptValueType.Number, 999)]), names), StringComparison.Ordinal);
			Assert.DoesNotContain("dante", ScrText.Render(BuildLine(ScriptConditionType.True, [], Action("SAT_CALL_SCRIPT"), [((Byte)ScriptValueType.GlVar, 1013)]), names), StringComparison.Ordinal);

			// The other instructions that name a script are not changed.
			Assert.DoesNotContain("dante", ScrText.Render(BuildLine(ScriptConditionType.True, [], Action("SAT_CHANGE_SCRIPT"), [((Byte)ScriptFocusObject.Attachee, 0), ((Byte)ScriptValueType.Number, 1013)]), names), StringComparison.Ordinal);
		}

		[Fact]
		public void TheNameOfAScript_IsTheFileThatStartsWithItsNumber_InTheArchivesThatAreOpen()
		{
			Assert.Equal(1013, ScrScriptNames.NumberOf("01013dante.scr"));
			Assert.Equal(39990, ScrScriptNames.NumberOf("39990.scr"));
			Assert.Equal(7, ScrScriptNames.NumberOf("7x.scr"));
			Assert.Null(ScrScriptNames.NumberOf("dante.scr"));
			Assert.Null(ScrScriptNames.NumberOf("12345678901.scr"));

			String root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DatExplorerIII.Tests", System.IO.Path.GetRandomFileName());
			try
			{
				String first = System.IO.Path.Combine(root, "first");
				String second = System.IO.Path.Combine(root, "second");
				System.IO.Directory.CreateDirectory(System.IO.Path.Combine(first, "scr"));
				System.IO.Directory.CreateDirectory(System.IO.Path.Combine(second, "scr"));
				System.IO.File.WriteAllText(System.IO.Path.Combine(first, "scr", "01013dante.scr"), "x");
				System.IO.File.WriteAllText(System.IO.Path.Combine(first, "scr", "notes.txt"), "x");
				System.IO.File.WriteAllText(System.IO.Path.Combine(second, "scr", "01013other.scr"), "x");
				System.IO.File.WriteAllText(System.IO.Path.Combine(second, "scr", "39990.SCR"), "x");

				using DatExplorerIII.Core.DatArchive one = DatExplorerIII.Core.DatArchive.OpenFolder(first);
				using DatExplorerIII.Core.DatArchive two = DatExplorerIII.Core.DatArchive.OpenFolder(second);
				Func<Int32, String?> names = ScrScriptNames.For([one, two]);

				// The first archive gives the name when both have the number.
				Assert.Equal("01013dante.scr", names(1013));
				Assert.Equal("39990.SCR", names(39990));
				Assert.Null(names(5));
			}
			finally
			{
				System.IO.Directory.Delete(root, true);
			}
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