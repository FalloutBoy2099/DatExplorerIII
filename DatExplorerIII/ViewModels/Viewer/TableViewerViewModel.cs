namespace DatExplorerIII.ViewModels.Viewer
{
	using System;
	using System.Collections.Generic;
	using System.Data;
	using System.Globalization;
	using Infrastructure;
	using Models;
	using Services;

	/// <summary>
	/// The content of a message file (.mes) or of a dialog (.dlg) shown as a table: a line for each message, that can be
	/// sorted by any column.
	/// </summary>
	public sealed class TableViewerViewModel : ObservableObject
	{
		/// <summary>The names of the columns of the table: they are what a cell is found by; the header is their text in the language.</summary>
		internal const String NumberColumn = "Number";
		internal const String KindColumn = "Kind";
		internal const String TextColumn = "Text";
		internal const String Text2Column = "Text2";
		internal const String IqColumn = "Iq";
		internal const String TestColumn = "Test";
		internal const String GotoColumn = "Goto";
		internal const String EffectColumn = "Effect";
		internal const String VariableColumn = "Variable";
		internal const String ValueColumn = "Value";
		internal const String CommentColumn = "Comment";
		internal const String FrameColumn = "Frame";

		/// <summary>A column that is not shown: true for a line that is a comment between the lines of the file (the table colors it).</summary>
		internal const String IsCommentColumn = "IsComment";

		private static readonly Dictionary<String, String> HeaderKeys = new()
		{
			[NumberColumn] = "Str.TableNumber",
			[KindColumn] = "Str.TableKind",
			[TextColumn] = "Str.TableText",
			[IqColumn] = "Str.TableIq",
			[TestColumn] = "Str.TableConditions",
			[GotoColumn] = "Str.TableGoto",
			[EffectColumn] = "Str.TableEffect",
			[VariableColumn] = "Str.TableVariable",
			[ValueColumn] = "Str.TableValue",
			[CommentColumn] = "Str.TableComment",
			[FrameColumn] = "Str.TableFrame",
		};

		private MesFile? _messages;
		private DlgFile? _dialog;
		private GamFile? _variables;
		private SveFile? _subtitles;
		private DataView? _rows;
		private String _status = String.Empty;
		private String _notice = String.Empty;
		private Boolean _showLineComments;
		private Boolean _showBlockComments;

		/// <summary>Whether the comments at the end of the lines of the file are shown (a column).</summary>
		public Boolean ShowLineComments
		{
			get => _showLineComments;
			set
			{
				if (SetProperty(ref _showLineComments, value))
				{
					RefreshComments();
				}
			}
		}

		/// <summary>Whether the comments between the lines of the file are shown (a line of the table for each).</summary>
		public Boolean ShowBlockComments
		{
			get => _showBlockComments;
			set
			{
				if (SetProperty(ref _showBlockComments, value))
				{
					OnPropertyChanged(nameof(HasCommentRows));
					RefreshComments();
				}
			}
		}

		/// <summary>The kinds of file that can have comments (message files and dialogs) offer the choice to show them.</summary>
		public Boolean HasComments => _messages != null || _dialog != null;

		/// <summary>True when the table has lines that are comments, so that a column marks them (the view colors them by it).</summary>
		public Boolean HasCommentRows => _showBlockComments && HasComments;

		private void RefreshComments()
		{
			if (HasComments)
			{
				Build();
			}
		}

		/// <summary>The lines of the table.</summary>
		public DataView? Rows
		{
			get => _rows;
			private set => SetProperty(ref _rows, value);
		}

		/// <summary>How many lines there are, and the encoding.</summary>
		public String Status
		{
			get => _status;
			private set => SetProperty(ref _status, value);
		}

		/// <summary>A remark: some of the file could not be read.</summary>
		public String Notice
		{
			get => _notice;
			private set => SetProperty(ref _notice, value);
		}

		/// <summary>The text of the header of a column, in the language of the interface.</summary>
		public String GetHeader(String column)
		{
			if (column == Text2Column)
			{
				return Loc.Get(_dialog != null ? "Str.TableText2Dialog" : "Str.TableText");
			}

			return HeaderKeys.TryGetValue(column, out String? key) ? Loc.Get(key) : column;
		}

		public void Show(MesFile messages)
		{
			Clear();
			_messages = messages;
			OnPropertyChanged(nameof(HasComments));
			OnPropertyChanged(nameof(HasCommentRows));
			Build();
		}

		public void Show(DlgFile dialog)
		{
			Clear();
			_dialog = dialog;
			OnPropertyChanged(nameof(HasComments));
			OnPropertyChanged(nameof(HasCommentRows));
			Build();
		}

		public void Show(GamFile variables)
		{
			Clear();
			_variables = variables;
			Build();
		}

		public void Show(SveFile subtitles)
		{
			Clear();
			_subtitles = subtitles;
			Build();
		}

		public void Clear()
		{
			_messages = null;
			_dialog = null;
			_variables = null;
			_subtitles = null;
			OnPropertyChanged(nameof(HasComments));
			OnPropertyChanged(nameof(HasCommentRows));
			Rows = null;
			Status = String.Empty;
			Notice = String.Empty;
		}

		/// <summary>Writes the table again after the language of the interface changed.</summary>
		public void RefreshLanguage()
		{
			if (_messages != null || _dialog != null || _variables != null || _subtitles != null)
			{
				Build();
			}
		}

		/// <summary>The mark of the lines that are comments: a column of the table only when the comments between the lines are shown.</summary>
		private void AddIsCommentColumn(DataTable table)
		{
			if (_showBlockComments)
			{
				table.Columns.Add(IsCommentColumn, typeof(Boolean));
			}
		}

		/// <summary>The cells of a line of an entry, with the mark that says it is not a comment when the table has that column.</summary>
		private Object[] WithMark(Object[] cells)
		{
			return _showBlockComments ? [.. cells, false] : cells;
		}

		/// <summary>
		/// Adds the lines of the entries, and, when they are shown, the comments between them where they stand in the file. A comment
		/// is a line with no number whose text is the comment, in the column of the text.
		/// </summary>
		private void AddRows(DataTable table, Int32 count, IReadOnlyList<FileComment> comments, Func<Int32, Object[]> entryCells)
		{
			Int32 next = 0;

			void AddComments(Int32 entriesBefore)
			{
				while (next < comments.Count && comments[next].EntriesBefore <= entriesBefore)
				{
					if (_showBlockComments)
					{
						Object[] cells = new Object[table.Columns.Count];
						Array.Fill(cells, DBNull.Value);
						cells[table.Columns.IndexOf(TextColumn)] = "// " + comments[next].Text;
						cells[table.Columns.IndexOf(IsCommentColumn)] = true;
						table.Rows.Add(cells);
					}

					next++;
				}
			}

			for (Int32 index = 0; index < count; index++)
			{
				AddComments(index);
				table.Rows.Add(entryCells(index));
			}

			AddComments(count);
		}

		private void Build()
		{
			// The order the user chose (a column header) is kept when the table is written again; a column that is gone is not sorted by.
			String sort = Rows?.Sort ?? String.Empty;
			DataTable table = new();
			table.Columns.Add(NumberColumn, typeof(Int32));
			Int32 count;
			Int32 skipped;
			String encoding;

			if (_messages != null)
			{
				table.Columns.Add(TextColumn, typeof(String));
				if (_showLineComments)
				{
					table.Columns.Add(CommentColumn, typeof(String));
				}

				AddIsCommentColumn(table);
				AddRows(table, _messages.Entries.Count, _messages.Comments, index =>
				{
					MesEntry entry = _messages.Entries[index];
					return WithMark(_showLineComments ? [entry.Number, entry.Text, entry.Comment] : [entry.Number, entry.Text]);
				});

				count = _messages.Entries.Count;
				skipped = _messages.SkippedCount;
				encoding = _messages.EncodingName;
			}
			else if (_variables != null)
			{
				table.Columns.Add(VariableColumn, typeof(String));
				table.Columns.Add(ValueColumn, typeof(String));
				table.Columns.Add(CommentColumn, typeof(String));
				foreach (GamVariable variable in _variables.Variables)
				{
					table.Rows.Add(variable.Index, variable.Name, variable.Value, variable.Comment);
				}

				count = _variables.Variables.Count;
				skipped = _variables.SkippedCount;
				encoding = _variables.EncodingName;
			}
			else if (_subtitles != null)
			{
				table.Columns.Remove(NumberColumn);
				table.Columns.Add(FrameColumn, typeof(Int32));
				table.Columns.Add(TextColumn, typeof(String));
				foreach (SveEntry entry in _subtitles.Entries)
				{
					table.Rows.Add(entry.Frame, entry.Text);
				}

				count = _subtitles.Entries.Count;
				skipped = _subtitles.SkippedCount;
				encoding = _subtitles.EncodingName;
			}
			else
			{
				DlgFile dialog = _dialog!;
				table.Columns.Add(KindColumn, typeof(String));
				table.Columns.Add(TextColumn, typeof(String));
				table.Columns.Add(Text2Column, typeof(String));
				table.Columns.Add(IqColumn, typeof(String));
				table.Columns.Add(TestColumn, typeof(String));
				table.Columns.Add(GotoColumn, typeof(String));
				table.Columns.Add(EffectColumn, typeof(String));
				if (_showLineComments)
				{
					table.Columns.Add(CommentColumn, typeof(String));
				}

				AddIsCommentColumn(table);

				String npc = Loc.Get("Str.TableKindNpc");
				String option = Loc.Get("Str.TableKindOption");
				AddRows(table, dialog.Entries.Count, dialog.Comments, index =>
				{
					DlgEntry entry = dialog.Entries[index];
					Object[] cells = [entry.Number, entry.IsPlayerOption ? option : npc, entry.Text, entry.Text2, entry.Iq, entry.Test, entry.Goto, entry.Effect];
					return WithMark(_showLineComments ? [.. cells, entry.Comment] : cells);
				});

				count = dialog.Entries.Count;
				skipped = dialog.SkippedCount;
				encoding = dialog.EncodingName;
			}

			DataView view = table.DefaultView;
			if (sort.Length > 0)
			{
				try
				{
					view.Sort = sort;
				}
				catch (IndexOutOfRangeException)
				{
					// The column it was sorted by is not in the table any more.
				}
			}

			Rows = view;
			Status = Loc.Format("Str.TableStatus", count.ToString("N0", CultureInfo.CurrentCulture), encoding);
			Notice = skipped > 0 ? Loc.Format("Str.TableSkipped", skipped.ToString("N0", CultureInfo.CurrentCulture)) : String.Empty;
		}
	}
}