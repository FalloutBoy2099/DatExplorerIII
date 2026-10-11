namespace DatExplorerIII.ViewModels
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
			Build();
		}

		public void Show(DlgFile dialog)
		{
			Clear();
			_dialog = dialog;
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

		private void Build()
		{
			DataTable table = new();
			table.Columns.Add(NumberColumn, typeof(Int32));
			Int32 count;
			Int32 skipped;
			String encoding;

			if (_messages != null)
			{
				table.Columns.Add(TextColumn, typeof(String));
				foreach (MesEntry entry in _messages.Entries)
				{
					table.Rows.Add(entry.Number, entry.Text);
				}

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

				String npc = Loc.Get("Str.TableKindNpc");
				String option = Loc.Get("Str.TableKindOption");
				foreach (DlgEntry entry in dialog.Entries)
				{
					table.Rows.Add(entry.Number, entry.IsPlayerOption ? option : npc, entry.Text, entry.Text2, entry.Iq, entry.Test, entry.Goto, entry.Effect);
				}

				count = dialog.Entries.Count;
				skipped = dialog.SkippedCount;
				encoding = dialog.EncodingName;
			}

			Rows = table.DefaultView;
			Status = Loc.Format("Str.TableStatus", count.ToString("N0", CultureInfo.CurrentCulture), encoding);
			Notice = skipped > 0 ? Loc.Format("Str.TableSkipped", skipped.ToString("N0", CultureInfo.CurrentCulture)) : String.Empty;
		}
	}
}