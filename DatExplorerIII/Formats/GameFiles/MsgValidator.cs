namespace DatExplorerIII.Formats.GameFiles
{
	using System;
	using System.Collections.Generic;
	using System.Text;
	using Models.GameFiles;

	/// <summary>
	/// Looks for what is wrong in the text of a message file (.msg) in a single pass: nothing is allocated for a message that is
	/// right but its text. The format is <c>{number}{sound}{text}</c>, the text may run over several lines, and a line that starts
	/// with <c>#</c> outside the braces is a comment.
	/// </summary>
	public static class MsgValidator
	{
		/// <summary>The longest text of a message, in characters (line breaks not counted).</summary>
		public const Int32 MaxTextLength = 1024;

		/// <summary>The longest word, in characters: a longer one does not fit in a line of the game.</summary>
		public const Int32 MaxWordLength = 53;

		private const Int32 ExcerptLength = 120;

		/// <summary>Finds the problems of a message file.</summary>
		/// <param name="text">The text of the file, decoded.</param>
		/// <returns>The problems, in the order of the file.</returns>
		public static IReadOnlyList<MsgIssue> Validate(String text)
		{
			// A Ctrl+Z (0x1A) at the end is the end-of-file mark of the old DOS editors: the C library of the game stops reading there
			// (a file in text mode), so it is not a part of the file. (The decoder of the texts shows the control characters as their
			// picture: this one is then U+241A.)
			Checker checker = new(text.TrimEnd('\u001A', '\u241A'));
			checker.Run();
			return checker.Issues;
		}

		private sealed class Checker
		{
			private readonly String _text;
			private readonly List<Int32> _lineStarts = [0];
			private readonly Dictionary<UInt32, Int32> _ids = [];
			private readonly StringBuilder _field = new();
			private readonly List<(MsgIssueKind Kind, Int32 Line, Int32 Position, Int64 Value, Int64 Other)> _found = [];
			private Int32 _position;

			public Checker(String text)
			{
				_text = text;
				Issues = Array.Empty<MsgIssue>();
			}

			public IReadOnlyList<MsgIssue> Issues { get; private set; }

			private Int32 Line => _lineStarts.Count;

			public void Run()
			{
				String text = _text;
				if (text.Length > 0 && text[0] == '﻿')
					_position = 1;

				while (_position < text.Length)
				{
					Char c = text[_position];
					if (c == '\n')
					{
						NewLine();
					}
					else if (c is ' ' or '\t' or '\r')
					{
						_position++;
					}
					else if (c == '#')
					{
						SkipLine();
					}
					else if (c == '}')
					{
						Report(MsgIssueKind.StrayCloseBrace, Line, _position);
						_position++;
					}
					else if (c == '{')
					{
						ReadMessage();
					}
					else
					{
						// What is between the messages is ignored by the game, a comment or not.
						_position++;
					}
				}

				if (text.Length > 0 && text[^1] != '\n')
					Report(MsgIssueKind.NoFinalEmptyLine, Line, text.Length - 1);

				Issues = Build();
			}

			private void NewLine()
			{
				_position++;
				_lineStarts.Add(_position);
			}

			private void SkipLine()
			{
				while (_position < _text.Length && _text[_position] != '\n')
				{
					_position++;
				}
			}

			private void Report(MsgIssueKind kind, Int32 line, Int32 position, Int64 value = 0, Int64 other = 0)
			{
				_found.Add((kind, line, position, value, other));
			}

			/// <summary>Reads a message from its first brace: up to three fields, then the end of its line.</summary>
			private void ReadMessage()
			{
				String text = _text;
				Int32 entryLine = Line;
				Int32 entryPosition = _position;
				String[] fields = new String[3];
				Int32[] fieldLines = new Int32[3];
				Int32 count = 0;

				while (true)
				{
					Int32 fieldLine = Line;
					Int32 fieldPosition = _position;
					_position++;
					_field.Clear();
					while (_position < text.Length && text[_position] != '}')
					{
						Char c = text[_position];
						if (c == '{')
						{
							// Another message starts here: the closing brace of this field is missing.
							Report(MsgIssueKind.UnexpectedOpenBrace, Line, _position);
							return;
						}

						if (c == '\n')
						{
							_field.Append('\n');
							NewLine();
							continue;
						}

						if (c != '\r')
							_field.Append(c);

						_position++;
					}

					if (_position >= text.Length)
					{
						Report(MsgIssueKind.UnclosedBrace, fieldLine, fieldPosition);
						return;
					}

					_position++;
					if (count < 3)
					{
						fields[count] = _field.ToString();
						fieldLines[count] = fieldLine;
					}

					count++;
					while (_position < text.Length && text[_position] is ' ' or '\t' or '\r')
					{
						_position++;
					}

					if (_position >= text.Length || text[_position] != '{')
						break;
				}

				Int32 closingLine = Line;
				Int32 closingPosition = _position - 1;
				if (count != 3)
				{
					Report(MsgIssueKind.WrongFieldCount, entryLine, entryPosition, count);
					return;
				}

				CheckMessage(fields, fieldLines, closingLine, closingPosition);
			}

			private void CheckMessage(String[] fields, Int32[] fieldLines, Int32 closingLine, Int32 closingPosition)
			{
				String id = fields[0].Trim();
				if (id.Length == 0 || !IsDigits(id) || !UInt32.TryParse(id, out UInt32 number))
				{
					Report(MsgIssueKind.InvalidId, fieldLines[0], _lineStarts[fieldLines[0] - 1], 0, 0);
				}
				else if (_ids.TryGetValue(number, out Int32 first))
				{
					Report(MsgIssueKind.DuplicateId, fieldLines[0], _lineStarts[fieldLines[0] - 1], number, first);
				}
				else
				{
					_ids[number] = fieldLines[0];
				}

				String body = fields[2];
				Int32 length = body.Length;
				Int32 lastBreak = -1;
				Int32 run = 0;
				Int32 line = fieldLines[2];
				for (Int32 i = 0; i < body.Length; i++)
				{
					Char c = body[i];
					if (c == '\n')
					{
						length--;
						lastBreak = i;
						line++;
					}

					if (Char.IsLetterOrDigit(c) || c == '_')
					{
						run++;
						if (run == MaxWordLength + 1)
						{
							// One report per word: counted when it ends, with its whole length.
							Int32 end = i + 1;
							while (end < body.Length && (Char.IsLetterOrDigit(body[end]) || body[end] == '_'))
							{
								end++;
							}

							Report(MsgIssueKind.WordTooLong, line, _lineStarts[line - 1], end - (i + 1 - run));
							i = end - 1;
							run = 0;
						}
					}
					else
					{
						run = 0;
					}
				}

				if (length > MaxTextLength)
					Report(MsgIssueKind.TextTooLong, fieldLines[2], _lineStarts[fieldLines[2] - 1], length);

				// Only spaces or line breaks on the last line of the text (an empty text is allowed).
				String last = lastBreak >= 0 ? body[(lastBreak + 1)..] : body;
				if (last.AsSpan().Trim().IsEmpty && (lastBreak >= 0 || last.Length > 0))
					Report(MsgIssueKind.BlankLastLine, closingLine, closingPosition);
			}

			private static Boolean IsDigits(String text)
			{
				foreach (Char c in text)
				{
					if (!Char.IsAsciiDigit(c))
						return false;
				}

				return true;
			}

			private List<MsgIssue> Build()
			{
				List<MsgIssue> issues = new(_found.Count);
				foreach ((MsgIssueKind kind, Int32 line, Int32 position, Int64 value, Int64 other) in _found)
				{
					Int32 start = line - 1 < _lineStarts.Count ? _lineStarts[line - 1] : 0;
					Int32 end = _text.IndexOf('\n', start);
					String excerpt = (end < 0 ? _text[start..] : _text[start..end]).Trim();
					if (excerpt.Length > ExcerptLength)
						excerpt = excerpt[..ExcerptLength] + "…";

					Int32 column = Math.Max(1, position - start + 1);
					issues.Add(new MsgIssue(MsgIssue.SeverityOf(kind), kind, line, column, value, other, excerpt));
				}

				issues.Sort(static (a, b) => a.Line != b.Line ? a.Line.CompareTo(b.Line) : a.Column.CompareTo(b.Column));
				return issues;
			}
		}
	}
}